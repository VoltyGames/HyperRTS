namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>How a launch runs: one local world, a host, or a dedicated server.</summary>
    public enum LaunchNetwork : byte
    {
        Offline,
        Host,
        Dedicated,
    }
}
