using Unity.Netcode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Server to client: the join seated it as an observer. A seated player hears back through its player ghost's
    /// <c>GhostOwner</c> instead.
    /// </summary>
    public struct ObserverAccepted : IRpcCommand
    {
    }
}
