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

const releasePlugins = [
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
];

const dryRunPlugins = [
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
  ]
];

// "staging" is deliberately NOT listed as a semantic-release prerelease branch here (it used
// to be, via { name: "staging", prerelease: "RC" }): RC version determination for staging now
// lives in staging-ci.yml's own "version" job, which invokes semantic-release with a
// --branches override against this same config instead of a second branch entry in this file
// (see ci-target-schema.md section 4.8). Dropping that entry also resolves the previous
// uppercase "RC" prerelease identifier, which conflicted with the project-wide lowercase
// vX.Y.Z-rc.N tag format (decision 1).
module.exports = {
  branches: ["main"],
  tagFormat: "v${version}",
  plugins: process.env.RESOLVE_DRY_RUN === "true" ? dryRunPlugins : releasePlugins
};
