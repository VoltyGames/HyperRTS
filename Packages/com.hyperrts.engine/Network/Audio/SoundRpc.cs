using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Network.Audio
{
    /// <summary>A <c>SoundEvent</c> on the wire; the client plays it from its own copy of the prefab.</summary>
    public struct SoundRpc : IRpcCommand
    {
        public int TypeId;
        public byte Slot;
        public byte Faction;
        public float3 Position;
    }
}
