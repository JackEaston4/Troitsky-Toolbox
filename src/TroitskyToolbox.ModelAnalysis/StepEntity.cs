namespace TroitskyToolbox.ModelAnalysis;



/// <summary>
/// One entity instance from the DATA section. A simple instance has a single part
/// (<c>#1=CARTESIAN_POINT(...)</c>); a complex instance has several
/// (<c>#2=(LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.MILLI.,.METRE.))</c>).
/// </summary>
public sealed record StepEntity(int Id, IReadOnlyList<StepTyped> Parts)
{
    public bool Is(string type) => Parts.Any(p => p.Name == type);

    public StepTyped? Part(string type) => Parts.FirstOrDefault(p => p.Name == type);
}