namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes the result of an update operation performed by <see cref="IAutoUpdateOrchestrator"/> or
/// <see cref="IAutoUpdateCommandHandler"/>.
/// </summary>
/// <param name="Outcome">The outcome of the operation.</param>
/// <param name="State">The <see cref="AutoUpdateState"/> after the operation completed.</param>
/// <param name="Message">A human-readable message describing the result.</param>
/// <param name="Error">The exception that caused a <see cref="AutoUpdateOutcome.Failed"/> outcome, if any.</param>
/// <returns>An immutable result describing the outcome of the operation.</returns>
public sealed record AutoUpdateResult(
    AutoUpdateOutcome Outcome,
    AutoUpdateState State,
    string? Message,
    Exception? Error);
