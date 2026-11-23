namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>One line of a unit's details: a muted name and its value.</summary>
    public readonly struct DetailRow
    {
        public readonly string Name;
        public readonly string Value;

        public DetailRow(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
