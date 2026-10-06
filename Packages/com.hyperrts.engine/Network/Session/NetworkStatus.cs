namespace HyperRTS.Network.Session
{
    /// <summary>Connection state of this process's session, for lobby and connection UI.</summary>
    public enum NetworkStatus
    {
        /// <summary>No session: single player.</summary>
        Idle,
        Connecting,
        /// <summary>The client joined its server; a dedicated server is listening.</summary>
        Connected,
        /// <summary>The connection failed or closed (<see cref="NetworkSession.DisconnectReason"/>); call <c>Stop</c>.</summary>
        Disconnected,
    }
}
