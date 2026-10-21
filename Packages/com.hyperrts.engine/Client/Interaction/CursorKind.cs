namespace HyperRTS.Client.Interaction
{
    /// <summary>What the pointer would do if clicked now, which picks the cursor the player sees.</summary>
    public enum CursorKind : byte
    {
        Default,
        Select,
        AttackMove,
        Attack,
        Target,
        Capture,
        Garrison,
        Unload,
        Repair,
        Gather,
        Build,
        Rally,
        Invalid,
        ScrollUp,
        ScrollDown,
        ScrollLeft,
        ScrollRight,
        ScrollUpLeft,
        ScrollUpRight,
        ScrollDownLeft,
        ScrollDownRight,
    }
}
