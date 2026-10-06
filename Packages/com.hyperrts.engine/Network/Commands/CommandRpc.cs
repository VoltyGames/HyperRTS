using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// A <c>PlayerCommand</c> on the wire. Netcode sends entities as ghost references (id and spawn tick), so a
    /// despawned ghost arrives as <c>Entity.Null</c> rather than as a newer ghost reusing its id. Prefabs travel as
    /// <c>EntityInfo.TypeId</c>; selection is client-side, so the commanded units come along.
    /// </summary>
    public struct CommandRpc : IRpcCommand
    {
        /// <summary>Most units one command carries; the rest of a larger selection is dropped.</summary>
        public const int MaxSubjects = 127;

        public byte Type;
        public bool Queue;
        public int Argument;
        public float3 Position;
        public Entity Target;
        public int PrefabTypeId;

        [GhostFixedListCapacity(Capacity = MaxSubjects)]
        public FixedList4096Bytes<Entity> Subjects;
    }
}
