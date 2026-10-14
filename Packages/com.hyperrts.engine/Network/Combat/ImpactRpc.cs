using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Network.Combat
{
    /// <summary>An <c>ImpactEvent</c> on the wire; the client finds the marks through its own copy of the prefab.</summary>
    public struct ImpactRpc : IRpcCommand
    {
        public int TypeId;
        public float3 Position;
        public float Radius;
        public byte Faction;
        public byte Surface;
        public byte Material;
    }
}
