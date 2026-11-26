namespace TroitskyToolbox.ModelAnalysis;

/// <summary>A typed parameter such as <c>LENGTH_MEASURE(1.0)</c>, or one part of a complex entity.</summary>
public sealed record StepTyped(string Name, IReadOnlyList<object?> Args);
