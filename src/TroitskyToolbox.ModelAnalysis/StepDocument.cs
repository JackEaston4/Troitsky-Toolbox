using System;
using System.Collections.Generic;
using System.Text;

namespace TroitskyToolbox.ModelAnalysis;

/// <summary>The parsed contents of a STEP Part 21 file.</summary>
public sealed class StepDocument
{
    public required IReadOnlyList<object?> FileName { get; init; }
    public required IReadOnlyList<object?> FileSchema { get; init; }
    public required IReadOnlyDictionary<int, StepEntity> Entities { get; init; }

    public StepEntity? Get(object? value) =>
        value is StepRef r && Entities.TryGetValue(r.Id, out var e) ? e : null;
}