namespace TroitskyToolbox.ModelAnalysis;

/// <summary>Why a file was rejected, with a message that can be shown to the member (FR-MA-1).</summary>
public sealed record StepError(StepErrorKind Kind, string Message);
