namespace HyperRTS.Network.Session
{
    /// <summary>Which session a command line starts (<see cref="LaunchOptions"/>).</summary>
    public enum LaunchMode : byte
    {
        None = 0,
        Server = 1,
        Host = 2,
        Client = 3,
    }
}
