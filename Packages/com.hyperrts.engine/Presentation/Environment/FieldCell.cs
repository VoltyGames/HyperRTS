namespace HyperRTS.Presentation.Environment
{
    /// <summary>One metre of ground as <see cref="FieldFires"/> sees it: whether it can burn, burns now or has burnt.</summary>
    public enum FieldCell : byte
    {
        /// <summary>Roads, water, bare soil and anything else a fire stops at.</summary>
        Bare = 0,
        Dry = 1,
        Burning = 2,
        Burnt = 3,
    }
}
