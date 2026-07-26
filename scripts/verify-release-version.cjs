module.exports = {
  verifyRelease: (_, context) => {
    const expectedVersion = process.env.RELEASE_VERSION;

    if (!expectedVersion) {
      if (context.options.dryRun) {
        return;
      }

      throw new Error("RELEASE_VERSION must be set before Semantic Release publishes a release.");
    }

    if (context.nextRelease.version !== expectedVersion) {
      throw new Error(
        `Semantic Release resolved ${context.nextRelease.version}, but the prepared archive is for ${expectedVersion}.`
      );
    }
  }
};
