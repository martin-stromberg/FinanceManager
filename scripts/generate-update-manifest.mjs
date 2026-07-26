import { createHash } from "node:crypto";
import { readFileSync, statSync, writeFileSync } from "node:fs";
import { basename, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const platforms = [
  { platform: "windows", runtimeIdentifier: "win-x64" },
  { platform: "linux", runtimeIdentifier: "linux-x64" }
];

export function releaseAssetName(version, runtimeIdentifier) {
  return `FinanceManager-v${version}-${runtimeIdentifier}.zip`;
}

export function releaseMetadata({ version, publishedAt, commitSha, repository, runtimeIdentifier }) {
  return {
    version,
    publishedAt,
    commitSha,
    repository,
    runtimeIdentifier
  };
}

export function sha256File(path) {
  const hash = createHash("sha256");
  const data = statSync(path);
  if (!data.isFile() || data.size <= 0) {
    throw new Error(`Release asset '${path}' is missing or empty.`);
  }

  hash.update(readFileSync(path));
  return hash.digest("hex");
}

export function createUpdateManifest({
  version,
  releaseNotes,
  publishedAt,
  repository,
  assetDirectory = process.cwd()
}) {
  if (!version) {
    throw new Error("version must be set.");
  }
  if (!releaseNotes) {
    throw new Error("releaseNotes must be set.");
  }
  if (!publishedAt || Number.isNaN(Date.parse(publishedAt))) {
    throw new Error("publishedAt must be a valid ISO timestamp.");
  }
  if (!/^[^/\s]+\/[^/\s]+$/.test(repository ?? "")) {
    throw new Error("repository must be in owner/name format.");
  }

  const [repositoryOwner, repositoryName] = repository.split("/");
  const assets = platforms.map((item) => {
    const assetName = releaseAssetName(version, item.runtimeIdentifier);
    const assetPath = resolve(assetDirectory, assetName);
    const sizeBytes = statSync(assetPath).size;
    const sha256 = sha256File(assetPath);
    return {
      platform: item.platform,
      runtimeIdentifier: item.runtimeIdentifier,
      assetName,
      assetUrl: `https://github.com/${repository}/releases/download/v${version}/${assetName}`,
      sha256,
      sizeBytes
    };
  });

  return {
    version,
    releaseNotes,
    publishedAt,
    repositoryOwner,
    repositoryName,
    assets
  };
}

export function writeUpdateManifest({ outputPath = "update.json", ...input }) {
  const manifest = createUpdateManifest(input);
  writeFileSync(outputPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
  return outputPath;
}

export function main(environment = process.env) {
  const outputPath = environment.UPDATE_MANIFEST_PATH ?? "update.json";
  const publishedAt = environment.RELEASE_PUBLISHED_AT?.trim() || new Date().toISOString();
  writeUpdateManifest({
    outputPath,
    version: environment.RELEASE_VERSION,
    releaseNotes: environment.RELEASE_NOTES,
    publishedAt,
    repository: environment.GITHUB_REPOSITORY,
    assetDirectory: environment.RELEASE_ASSET_DIRECTORY ?? process.cwd()
  });
  console.log(`Wrote ${basename(outputPath)}`);
}

const isMainModule = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMainModule) {
  main();
}
