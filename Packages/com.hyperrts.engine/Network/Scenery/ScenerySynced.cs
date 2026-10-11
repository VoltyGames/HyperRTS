using Unity.Entities;

namespace HyperRTS.Network.Scenery
{
    /// <summary>Server, on a connection: it has been sent what its team knows of the scenery; news follows as seen.</summary>
    public struct ScenerySynced : IComponentData
    {
    }
}
