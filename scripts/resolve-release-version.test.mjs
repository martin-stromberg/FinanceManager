import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import {
  classifyWorkflowRef,
  listGitHubReleases,
  parseManualTag,
  parseNextReleaseVersion,
  releaseAssetName,
  resolveReleaseVersion
} from "./resolve-release-version.mjs";

const repository = "owner/repository";

function environment({ refType = "branch", refName = "master" } = {}) {
  return {
    GITHUB_OUTPUT: "test-output",
    GITHUB_REPOSITORY: repository,
    GITHUB_TOKEN: "test-token",
    GITHUB_REF_TYPE: refType,
    GITHUB_REF_NAME: refName
  };
}

function release(tag, assets = []) {
  return {
    tag_name: tag,
    assets: assets.map((asset) => typeof asset === "string"
      ? { name: asset, state: "uploaded", size: 1 }
      : asset)
  };
}

function effects(overrides = {}) {
  const output = [];
  return {
    output,
    dependencies: {
      getGitHubRelease: async () => null,
      listGitHubReleases: async () => [],
      remoteTagExists: () => false,
      runSemanticReleaseDryRun: () => "The next release version is 3.4.5",
      writeOutputs: (_path, values) => output.push(values),
      log: () => {},
      ...overrides
    }
  };
}

test("accepts strict manual release tags", () => {
  assert.equal(parseManualTag("v2.3.4"), "2.3.4");
  assert.deepEqual(classifyWorkflowRef({ refType: "tag", refName: "v2.3.4" }), {
    kind: "manual",
    version: "2.3.4",
    tag: "v2.3.4"
  });
});

test("rejects non-semantic manual tags", () => {
  assert.throws(() => parseManualTag("v2.3"), /valid vX\.Y\.Z/);
  assert.throws(() => parseManualTag("release-2.3.4"), /Expected a vX\.Y\.Z/);
  assert.throws(() => parseManualTag("v02.3.4"), /valid vX\.Y\.Z/);
});

test("accepts only master for automatic releases", () => {
  assert.deepEqual(classifyWorkflowRef({ refType: "branch", refName: "master" }), {
    kind: "automatic"
  });
  assert.throws(
    () => classifyWorkflowRef({ refType: "branch", refName: "develop" }),
    /Unsupported release ref/
  );
});

test("extracts a version only when Semantic Release announces one", () => {
  assert.equal(parseNextReleaseVersion("The next release version is 3.4.5"), "3.4.5");
  assert.equal(parseNextReleaseVersion("There are no relevant changes"), null);
});

test("creates a release for a manual tag without an existing release", async () => {
  const testEffects = effects();

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.deepEqual(testEffects.output, [{
    released: "true",
    reason: "manual-tag",
    version: "2.3.4",
    tag: "v2.3.4",
    release_kind: "manual",
    release_action: "create"
  }]);
});

test("skips a manual tag whose release already has the expected asset", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v2.3.4", [releaseAssetName("2.3.4"), releaseAssetName("2.3.4", "linux-x64"), "update.json"])
  });

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "false");
  assert.equal(testEffects.output[0].reason, "existing-release");
});

test("repairs a manual release whose expected asset is still in starter state", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v2.3.4", [{
      name: releaseAssetName("2.3.4"),
      state: "starter",
      size: 1024
    }])
  });

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.equal(testEffects.output[0].release_action, "upload-existing");
});

test("repairs a manual release whose uploaded asset has zero size", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v2.3.4", [{
      name: releaseAssetName("2.3.4"),
      state: "uploaded",
      size: 0
    }])
  });

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.equal(testEffects.output[0].release_action, "upload-existing");
});

test("treats an uploaded asset with positive size as complete", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v2.3.4", [
      {
        name: releaseAssetName("2.3.4"),
        state: "uploaded",
        size: 1024
      },
      releaseAssetName("2.3.4", "linux-x64"),
      "update.json"
    ])
  });

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "false");
  assert.equal(testEffects.output[0].reason, "existing-release");
});

test("repairs a manual release whose expected asset is missing", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v2.3.4")
  });

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "true");
  assert.equal(testEffects.output[0].release_action, "upload-existing");
  assert.equal(testEffects.output[0].tag, "v2.3.4");
});

test("creates an automatic release when Semantic Release resolves a new version", async () => {
  const testEffects = effects();

  await resolveReleaseVersion(environment(), testEffects.dependencies);

  assert.deepEqual(testEffects.output[0], {
    released: "true",
    reason: "semantic-release",
    version: "3.4.5",
    tag: "v3.4.5",
    release_kind: "automatic",
    release_action: "create"
  });
});

test("creates an automatic release even when older releases are missing expected assets", async () => {
  const testEffects = effects({
    listGitHubReleases: async () => [release("v2.3.4"), release("v2.3.3")]
  });

  await resolveReleaseVersion(environment(), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "true");
  assert.equal(testEffects.output[0].reason, "semantic-release");
  assert.equal(testEffects.output[0].release_action, "create");
  assert.equal(testEffects.output[0].tag, "v3.4.5");
});

test("repairs one automatic release whose expected asset is missing when no new release is pending", async () => {
  const testEffects = effects({
    listGitHubReleases: async () => [release("v2.3.4"), release("v2.3.3")],
    runSemanticReleaseDryRun: () => "There are no relevant changes"
  });

  await resolveReleaseVersion(environment(), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "true");
  assert.equal(testEffects.output[0].release_action, "upload-existing");
  assert.equal(testEffects.output[0].tag, "v2.3.4");
});

test("skips automatic releases without releasable commits", async () => {
  const testEffects = effects({ runSemanticReleaseDryRun: () => "There are no relevant changes" });

  await resolveReleaseVersion(environment(), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "false");
  assert.equal(testEffects.output[0].reason, "no-release");
});

test("rejects an automatic release when its tag already exists", async () => {
  const testEffects = effects({ remoteTagExists: () => true });

  await assert.rejects(
    resolveReleaseVersion(environment(), testEffects.dependencies),
    /Tag 'v3\.4\.5' already exists/
  );
  assert.deepEqual(testEffects.output, []);
});

test("rejects an automatic release when its complete release already exists", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v3.4.5", [
      releaseAssetName("3.4.5"),
      releaseAssetName("3.4.5", "linux-x64"),
      "update.json"
    ])
  });

  await assert.rejects(
    resolveReleaseVersion(environment(), testEffects.dependencies),
    /GitHub release 'v3\.4\.5' already exists/
  );
  assert.deepEqual(testEffects.output, []);
});

test("lists GitHub releases across all pages", async () => {
  const requests = [];
  const firstPage = Array.from({ length: 100 }, (_, index) => release(`v1.0.${index}`));
  const releases = await listGitHubReleases({
    repository,
    token: "test-token",
    githubRequest: async ({ path }) => {
      requests.push(path);
      const page = path.endsWith("page=1") ? firstPage : [release("v2.0.0")];
      return { ok: true, status: 200, json: async () => page };
    }
  });

  assert.equal(releases.length, 101);
  assert.deepEqual(requests, ["/releases?per_page=100&page=1", "/releases?per_page=100&page=2"]);
});

test("uses one repository-wide release concurrency group", () => {
  const workflow = readFileSync(new URL("../.github/workflows/release.yml", import.meta.url), "utf8");

  assert.match(workflow, /concurrency:\r?\n  group: release-\$\{\{ github\.repository \}\}/);
  assert.doesNotMatch(workflow, /group: release-\$\{\{ github\.repository \}\}-\$\{\{ github\.ref \}\}/);
});

test("checks out the release tag before repairing an existing release asset", () => {
  const workflow = readFileSync(new URL("../.github/workflows/release.yml", import.meta.url), "utf8");

  assert.match(workflow, /Check out release tag for asset repair/);
  assert.match(workflow, /steps\.version\.outputs\.release_action == 'upload-existing'/);
  assert.match(workflow, /git checkout --detach \"\$env:RELEASE_TAG\"/);
  assert.match(workflow, /Release tag checkout did not resolve to the tagged commit/);
});

test("expects windows, linux and update manifest release assets", async () => {
  const testEffects = effects({
    getGitHubRelease: async () => release("v2.3.4", [
      releaseAssetName("2.3.4"),
      releaseAssetName("2.3.4", "linux-x64"),
      "update.json"
    ])
  });

  await resolveReleaseVersion(environment({ refType: "tag", refName: "v2.3.4" }), testEffects.dependencies);

  assert.equal(testEffects.output[0].released, "false");
});
