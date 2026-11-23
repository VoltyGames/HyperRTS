namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>One line of a unit's details: a muted name and its value, or a count of chevrons in its place.</summary>
    public readonly struct DetailRow
    {
        public readonly string Name;
        public readonly string Value;
        public readonly int Chevrons;

        public DetailRow(string name, string value)
        {
            Name = name;
            Value = value;
            Chevrons = 0;
        }

        public DetailRow(string name, int chevrons)
        {
            Name = name;
            Value = "";
            Chevrons = chevrons;
        }
    }
}
