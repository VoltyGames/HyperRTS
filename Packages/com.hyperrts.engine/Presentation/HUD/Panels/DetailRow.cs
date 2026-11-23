namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>One line of a unit's details: a muted name and its value, or a count of chevrons in its place.</summary>
    public readonly struct DetailRow
    {
        public readonly string Name;
        public readonly string Value;
        public readonly int Chevrons;

        public DetailRow(string name, string value, int chevrons = 0)
        {
            Name = name;
            Value = value;
            Chevrons = chevrons;
        }
    }
}
