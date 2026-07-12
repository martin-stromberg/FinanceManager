const path = require("node:path");

const releaseAssetPath = process.env.RELEASE_ASSET_PATH;

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
        assets: releaseAssetPath
          ? [{ path: releaseAssetPath, name: path.basename(releaseAssetPath) }]
          : []
      }
    ]
  ]
};
