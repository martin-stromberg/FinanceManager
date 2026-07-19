import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { createUpdateManifest, main, releaseAssetName } from "./generate-update-manifest.mjs";

test("creates update manifest with release notes and platform assets", () => {
  const dir = mkdtempSync(join(tmpdir(), "fm-release-"));
  try {
    writeFileSync(join(dir, releaseAssetName("1.2.3", "win-x64")), "windows");
    writeFileSync(join(dir, releaseAssetName("1.2.3", "linux-x64")), "linux");

    const manifest = createUpdateManifest({
      version: "1.2.3",
      releaseNotes: "Release notes",
      publishedAt: "2026-07-19T00:00:00Z",
      repository: "martin-stromberg/FinanceManager",
      assetDirectory: dir
    });

    assert.equal(manifest.repositoryOwner, "martin-stromberg");
    assert.equal(manifest.repositoryName, "FinanceManager");
    assert.equal(manifest.releaseNotes, "Release notes");
    assert.deepEqual(manifest.assets.map((asset) => asset.runtimeIdentifier), ["win-x64", "linux-x64"]);
    assert.ok(manifest.assets.every((asset) => asset.sha256.length === 64));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("rejects blank publishedAt", () => {
  const dir = mkdtempSync(join(tmpdir(), "fm-release-"));
  try {
    writeFileSync(join(dir, releaseAssetName("1.2.3", "win-x64")), "windows");
    writeFileSync(join(dir, releaseAssetName("1.2.3", "linux-x64")), "linux");

    assert.throws(
      () => createUpdateManifest({
        version: "1.2.3",
        releaseNotes: "Release notes",
        publishedAt: "",
        repository: "martin-stromberg/FinanceManager",
        assetDirectory: dir
      }),
      /publishedAt must be a valid ISO timestamp/
    );
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("main falls back to current ISO timestamp when RELEASE_PUBLISHED_AT is blank", () => {
  const dir = mkdtempSync(join(tmpdir(), "fm-release-"));
  try {
    writeFileSync(join(dir, releaseAssetName("1.2.3", "win-x64")), "windows");
    writeFileSync(join(dir, releaseAssetName("1.2.3", "linux-x64")), "linux");
    const outputPath = join(dir, "update.json");

    main({
      UPDATE_MANIFEST_PATH: outputPath,
      RELEASE_VERSION: "1.2.3",
      RELEASE_NOTES: "Release notes",
      RELEASE_PUBLISHED_AT: " ",
      GITHUB_REPOSITORY: "martin-stromberg/FinanceManager",
      RELEASE_ASSET_DIRECTORY: dir
    });

    const manifest = JSON.parse(readFileSync(outputPath, "utf8"));
    assert.ok(manifest.publishedAt);
    assert.ok(!Number.isNaN(Date.parse(manifest.publishedAt)));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
