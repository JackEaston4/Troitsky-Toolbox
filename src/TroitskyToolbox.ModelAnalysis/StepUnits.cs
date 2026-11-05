namespace TroitskyToolbox.ModelAnalysis;

/// <summary>Resolves STEP unit definitions (SI_UNIT, CONVERSION_BASED_UNIT, DERIVED_UNIT).</summary>
public static class StepUnits
{
    private enum Dim { Length, Mass, Other }

    private static readonly Dictionary<string, double> SiPrefix = new()
    {
        ["EXA"] = 1e18, ["PETA"] = 1e15, ["TERA"] = 1e12, ["GIGA"] = 1e9, ["MEGA"] = 1e6,
        ["KILO"] = 1e3, ["HECTO"] = 1e2, ["DECA"] = 1e1, ["DECI"] = 1e-1, ["CENTI"] = 1e-2,
        ["MILLI"] = 1e-3, ["MICRO"] = 1e-6, ["NANO"] = 1e-9, ["PICO"] = 1e-12,
    };

    private static readonly Dictionary<string, string> PrefixNames = new()
    {
        ["KILO"] = "kilo", ["CENTI"] = "centi", ["MILLI"] = "milli", ["MICRO"] = "micro", ["DECI"] = "deci",
    };

    /// <summary>
    /// Name of the length unit of the geometry context used by the solid model
    /// (e.g. "millimetre", "inch"), or null if none is declared.
    /// </summary>
    public static string? GeometryLengthUnit(StepDocument doc)
    {
        var context = GeometryContext(doc);
        var units = context?.Part("GLOBAL_UNIT_ASSIGNED_CONTEXT")?.Args.ElementAtOrDefault(0) as IReadOnlyList<object?>;
        var length = units?.Select(doc.Get).FirstOrDefault(u => u is not null && u.Is("LENGTH_UNIT"));
        if (length is null) return null;

        if (length.Part("SI_UNIT") is { } si)
        {
            var prefix = (si.Args.ElementAtOrDefault(0) as StepEnum?)?.Value;
            return (prefix is null ? "" : PrefixNames.GetValueOrDefault(prefix, prefix.ToLowerInvariant())) + "metre";
        }
        return length.Part("CONVERSION_BASED_UNIT")?.Args.ElementAtOrDefault(0) as string;
    }

    /// <summary>
    /// Material density stored by the CAD tool (a MEASURE_REPRESENTATION_ITEM named
    /// "density ..."), converted to g/cm³; null if absent or not a mass/volume unit.
    /// </summary>
    public static double? Density(StepDocument doc)
    {
        foreach (var e in doc.Entities.Values)
        {
            var item = e.Part("MEASURE_REPRESENTATION_ITEM");
            if (item?.Args.ElementAtOrDefault(0) is not string name ||
                !name.Contains("density", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = MeasureValue(item.Args.ElementAtOrDefault(1));
            var unit = doc.Get(item.Args.ElementAtOrDefault(2)) ?? doc.Get(e.Part("MEASURE_WITH_UNIT")?.Args.ElementAtOrDefault(1));
            if (value is null || unit?.Part("DERIVED_UNIT") is not { } derived) continue;

            // Express the unit in g and mm, then check it is mass¹·length⁻³.
            double factor = 1, massExp = 0, lengthExp = 0;
            var ok = true;
            foreach (var elementRef in derived.Args.ElementAtOrDefault(0) as IReadOnlyList<object?> ?? [])
            {
                var element = doc.Get(elementRef)?.Part("DERIVED_UNIT_ELEMENT");
                if (element?.Args.ElementAtOrDefault(1) is not double exp) { ok = false; break; }
                var (dim, f) = Resolve(doc, doc.Get(element.Args.ElementAtOrDefault(0)), depth: 0);
                if (dim == Dim.Mass) massExp += exp;
                else if (dim == Dim.Length) lengthExp += exp;
                else { ok = false; break; }
                factor *= Math.Pow(f, exp);
            }
            if (!ok || massExp != 1 || lengthExp != -3) continue;

            return value.Value * factor * 1000; // g/mm³ -> g/cm³
        }
        return null;
    }

    /// <summary>The representation context of the first shape representation that holds a solid.</summary>
    private static StepEntity? GeometryContext(StepDocument doc)
    {
        foreach (var e in doc.Entities.Values)
        {
            var rep = e.Part("ADVANCED_BREP_SHAPE_REPRESENTATION")
                      ?? e.Part("MANIFOLD_SURFACE_SHAPE_REPRESENTATION")
                      ?? e.Part("FACETED_BREP_SHAPE_REPRESENTATION");
            if (rep is not null && doc.Get(rep.Args.ElementAtOrDefault(2)) is { } ctx) return ctx;
        }
        return doc.Entities.Values.FirstOrDefault(e =>
            e.Is("GEOMETRIC_REPRESENTATION_CONTEXT") && e.Is("GLOBAL_UNIT_ASSIGNED_CONTEXT"));
    }

    /// <summary>Returns the dimension of a unit and its size in base units (mm for length, g for mass).</summary>
    private static (Dim, double) Resolve(StepDocument doc, StepEntity? unit, int depth)
    {
        if (unit is null || depth > 8) return (Dim.Other, 1);

        if (unit.Part("SI_UNIT") is { } si)
        {
            var prefix = (si.Args.ElementAtOrDefault(0) as StepEnum?)?.Value;
            var p = prefix is null ? 1 : SiPrefix.GetValueOrDefault(prefix, 1);
            return ((si.Args.ElementAtOrDefault(1) as StepEnum?)?.Value switch
            {
                "METRE" => (Dim.Length, p * 1000),
                "GRAM" => (Dim.Mass, p),
                _ => (Dim.Other, p),
            });
        }

        if (unit.Part("CONVERSION_BASED_UNIT") is { } conv && doc.Get(conv.Args.ElementAtOrDefault(1)) is { } mwu)
        {
            var part = mwu.Parts.FirstOrDefault(x => x.Name.EndsWith("MEASURE_WITH_UNIT", StringComparison.Ordinal));
            var value = MeasureValue(part?.Args.ElementAtOrDefault(0));
            var (dim, f) = Resolve(doc, doc.Get(part?.Args.ElementAtOrDefault(1)), depth + 1);
            return (dim, (value ?? 1) * f);
        }

        return (Dim.Other, 1);
    }

    private static double? MeasureValue(object? v) => v switch
    {
        double d => d,
        StepTyped { Args.Count: > 0 } t when t.Args[0] is double d => d,
        _ => null,
    };
}
