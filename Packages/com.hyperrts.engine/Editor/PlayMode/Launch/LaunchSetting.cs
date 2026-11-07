namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>An on or off editor setting a game adds to the launcher, such as a debug view of its text. Subclasses are discovered.</summary>
    public abstract class LaunchSetting
    {
        public abstract string Label { get; }

        public abstract string Tooltip { get; }

        public abstract bool Enabled { get; set; }
    }
}
