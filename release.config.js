const path = require("node:path");

const releaseAssetPath = process.env.RELEASE_ASSET_PATH;
const releaseAssetPaths = (process.env.RELEASE_ASSET_PATHS ?? "")
  .split(/[;\n]/)
  .map((value) => value.trim())
  .filter(Boolean);
const releaseManifestPath = process.env.RELEASE_MANIFEST_PATH;
const releaseAssets = [...releaseAssetPaths, releaseAssetPath, releaseManifestPath]
  .filter(Boolean)
  .map((assetPath) => ({ path: assetPath, name: path.basename(assetPath) }));

module.exports = {
  branches: ["master"],
  tagFormat: "v${version}",
  plugins: [
    [
      "@semantic-release/commit-analyzer",
      {
        preset: "conventionalcommits",
        releaseRules: [
          { breaking: true, release: "major" },
          { type: "feat", release: "minor" },
          { type: "fix", release: "patch" },
          { type: "docs", release: false },
          { type: "refactor", release: false },
          { type: "chore", release: false }
        ]
      }
    ],
    "./scripts/verify-release-version.cjs",
    [
      "@semantic-release/release-notes-generator",
      {
        preset: "conventionalcommits"
      }
    ],
    [
      "@semantic-release/github",
      {
        assets: releaseAssets
      }
    ]
  ]
};
