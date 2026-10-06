using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Server only, on a connection entity: the player slot it controls. Observers have none.</summary>
    public struct ConnectionPlayer : IComponentData
    {
        public Entity Player;
    }
}
