import { execFileSync, spawnSync } from "node:child_process";
import { appendFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const VERSION_PATTERN = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const NEXT_RELEASE_PATTERN = /The next release version is\s+(\d+\.\d+\.\d+)/i;

export function parseManualTag(tagName) {
  if (!tagName?.startsWith("v")) {
    throw new Error(`Expected a vX.Y.Z tag, received '${tagName ?? ""}'.`);
  }

  const version = tagName.slice(1);
  if (!VERSION_PATTERN.test(version)) {
    throw new Error(`Tag '${tagName}' is not a valid vX.Y.Z release tag.`);
  }

  return version;
}

export function parseNextReleaseVersion(output) {
  const normalizedOutput = output.replace(/\u001B\[[0-?]*[ -/]*[@-~]/g, "");
  const match = normalizedOutput.match(NEXT_RELEASE_PATTERN);
  return match?.[1] ?? null;
}

export function classifyWorkflowRef({ refType, refName }) {
  if (refType === "tag") {
    return { kind: "manual", version: parseManualTag(refName), tag: refName };
  }

  if (refType === "branch" && refName === "master") {
    return { kind: "automatic" };
  }

  throw new Error(`Unsupported release ref '${refType ?? ""}:${refName ?? ""}'.`);
}

export function releaseAssetName(version, runtimeIdentifier = "win-x64") {
  return `FinanceManager-v${version}-${runtimeIdentifier}.zip`;
}

export function expectedReleaseAssetNames(version) {
  return [
    releaseAssetName(version, "win-x64"),
    releaseAssetName(version, "linux-x64"),
    "update.json"
  ];
}

export function releaseHasExpectedAsset(release, version) {
  const assets = release.assets ?? [];
  return expectedReleaseAssetNames(version).every((assetName) =>
    assets.some((asset) => asset.name === assetName && asset.state === "uploaded" && asset.size > 0)
  );
}

function requireValue(value, name) {
  if (!value) {
    throw new Error(`${name} must be set.`);
  }

  return value;
}

function runSemanticReleaseDryRun() {
  const semanticReleaseBinary = resolve(
    "node_modules",
    "semantic-release",
    "bin",
    "semantic-release.js"
  );
  const result = spawnSync(process.execPath, [semanticReleaseBinary, "--dry-run"], {
    encoding: "utf8",
    env: { ...process.env, CI: "true" },
    stdio: ["ignore", "pipe", "pipe"]
  });

  if (result.error) {
    throw new Error(`Could not start Semantic Release dry-run.\n${result.error.message}`);
  }

  const output = [result.stdout, result.stderr].filter(Boolean).join("\n");
  if (result.status !== 0) {
    throw new Error(`Semantic Release dry-run failed.\n${output}`.trim());
  }

  return output;
}

function remoteTagExists(tag) {
  try {
    execFileSync("git", ["ls-remote", "--exit-code", "--tags", "origin", `refs/tags/${tag}`], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    });
    return true;
  } catch (error) {
    if (error.status === 2) {
      return false;
    }

    const details = [error.stdout, error.stderr].filter(Boolean).join("\n");
    throw new Error(`Could not check whether tag '${tag}' already exists.\n${details}`.trim());
  }
}

function releaseApiUrl(repository, path) {
  if (!/^[^/\s]+\/[^/\s]+$/.test(repository)) {
    throw new Error(`GITHUB_REPOSITORY '${repository}' is invalid.`);
  }

  return `https://api.github.com/repos/${repository}${path}`;
}

async function githubRequest({ repository, path, token }) {
  const response = await fetch(releaseApiUrl(repository, path), {
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${token}`,
      "X-GitHub-Api-Version": "2022-11-28"
    }
  });

  return response;
}

async function getGitHubRelease({ repository, tag, token }) {
  const response = await githubRequest({
    repository,
    path: `/releases/tags/${encodeURIComponent(tag)}`,
    token
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`GitHub release lookup for '${tag}' failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function listGitHubReleases({ repository, token, githubRequest: request = githubRequest }) {
  const releases = [];

  for (let page = 1; ; page += 1) {
    const response = await request({
      repository,
      path: `/releases?per_page=100&page=${page}`,
      token
    });

    if (!response.ok) {
      throw new Error(`GitHub release listing failed with HTTP ${response.status}.`);
    }

    const releasePage = await response.json();
    if (!Array.isArray(releasePage)) {
      throw new Error("GitHub release listing returned an invalid response.");
    }

    releases.push(...releasePage);
    if (releasePage.length < 100) {
      return releases;
    }
  }
}

function writeOutputs(outputPath, outputs) {
  const lines = Object.entries(outputs).map(([name, value]) => `${name}=${value ?? ""}`);
  appendFileSync(outputPath, `${lines.join("\n")}\n`, "utf8");
}

function createDefaultDependencies() {
  return {
    getGitHubRelease,
    listGitHubReleases,
    remoteTagExists,
    runSemanticReleaseDryRun,
    writeOutputs,
    log: console.log
  };
}

function releaseVersion(release) {
  try {
    return parseManualTag(release.tag_name);
  } catch {
    return null;
  }
}

function incompleteReleases(releases) {
  return releases.filter((release) => {
    const version = releaseVersion(release);
    return version && !releaseHasExpectedAsset(release, version);
  });
}

function releaseOutputs({ released, reason, version = "", tag = "", kind, action = "none" }) {
  return {
    released: String(released),
    reason,
    version,
    tag,
    release_kind: kind,
    release_action: action
  };
}

function repairRelease({ release, kind }) {
  const version = releaseVersion(release);
  return releaseOutputs({
    released: true,
    reason: "missing-release-asset",
    version,
    tag: release.tag_name,
    kind,
    action: "upload-existing"
  });
}

export async function resolveReleaseVersion(environment = process.env, dependencies = {}) {
  const effects = { ...createDefaultDependencies(), ...dependencies };
  const outputPath = requireValue(environment.GITHUB_OUTPUT, "GITHUB_OUTPUT");
  const repository = requireValue(environment.GITHUB_REPOSITORY, "GITHUB_REPOSITORY");
  const token = requireValue(environment.GITHUB_TOKEN, "GITHUB_TOKEN");
  const releaseRef = classifyWorkflowRef({
    refType: environment.GITHUB_REF_TYPE,
    refName: environment.GITHUB_REF_NAME
  });
  const emit = (outputs, message) => {
    effects.writeOutputs(outputPath, outputs);
    effects.log(`released=${outputs.released} (${message})`);
    return outputs;
  };

  if (releaseRef.kind === "manual") {
    const existingRelease = await effects.getGitHubRelease({
      repository,
      tag: releaseRef.tag,
      token
    });

    if (existingRelease) {
      if (!releaseHasExpectedAsset(existingRelease, releaseRef.version)) {
        return emit(repairRelease({ release: existingRelease, kind: releaseRef.kind }), `release '${releaseRef.tag}' is missing its asset`);
      }

      return emit(
        releaseOutputs({
          released: false,
          reason: "existing-release",
          version: releaseRef.version,
          tag: releaseRef.tag,
          kind: releaseRef.kind
        }),
        `GitHub release '${releaseRef.tag}' already exists`
      );
    }

    return emit(
      releaseOutputs({
        released: true,
        reason: "manual-tag",
        version: releaseRef.version,
        tag: releaseRef.tag,
        kind: releaseRef.kind,
        action: "create"
      }),
      `manual tag '${releaseRef.tag}'`
    );
  }

  const version = parseNextReleaseVersion(effects.runSemanticReleaseDryRun());
  if (version) {
    const tag = `v${version}`;
    const existingRelease = await effects.getGitHubRelease({ repository, tag, token });
    if (existingRelease) {
      if (!releaseHasExpectedAsset(existingRelease, version)) {
        return emit(repairRelease({ release: existingRelease, kind: releaseRef.kind }), `release '${tag}' is missing its asset`);
      }

      throw new Error(`GitHub release '${tag}' already exists and will not be overwritten.`);
    }

    if (effects.remoteTagExists(tag)) {
      throw new Error(`Tag '${tag}' already exists and will not be overwritten.`);
    }

    return emit(
      releaseOutputs({
        released: true,
        reason: "semantic-release",
        version,
        tag,
        kind: releaseRef.kind,
        action: "create"
      }),
      `Semantic Release resolved '${tag}'`
    );
  }

  const incomplete = incompleteReleases(await effects.listGitHubReleases({ repository, token }));
  if (incomplete.length >= 1) {
    const release = incomplete[0];
    return emit(repairRelease({ release, kind: releaseRef.kind }), `release '${release.tag_name}' is missing its asset`);
  }

  return emit(
    releaseOutputs({ released: false, reason: "no-release", kind: releaseRef.kind }),
    "no releasable commits"
  );
}

async function main(environment = process.env) {
  await resolveReleaseVersion(environment);
}

const isMainModule = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMainModule) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
