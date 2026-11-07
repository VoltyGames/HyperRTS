namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>Who plays a launcher slot: nobody, you or a bot.</summary>
    public enum LaunchSlotKind : byte
    {
        Closed,
        Human,
        Bot,
    }
}
