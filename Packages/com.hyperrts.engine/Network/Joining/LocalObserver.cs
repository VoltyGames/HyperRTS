using Unity.Entities;

namespace HyperRTS.Network.Joining
{
    /// <summary>Client singleton: the server seated this client as an observer, so it has no <c>LocalPlayer</c>.</summary>
    public struct LocalObserver : IComponentData
    {
    }
}
