namespace HyperRTS.Presentation.HUD.Panels;

/// <summary>One line of a unit's details: a muted name and its value, or a count of chevrons in its place.</summary>
public readonly struct DetailRow(string name, string value, int chevrons = 0)
{
    public readonly string Name = name;
    public readonly string Value = value;
    public readonly int Chevrons = chevrons;
}
