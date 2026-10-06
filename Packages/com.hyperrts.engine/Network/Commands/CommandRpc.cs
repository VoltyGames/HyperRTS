using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// A <c>PlayerCommand</c> on the wire. Entities travel as ghost ids and prefabs as <c>EntityInfo.TypeId</c>;
    /// selection is client-side, so the commanded units come along.
    /// </summary>
    public struct CommandRpc : IRpcCommand
    {
        /// <summary>Most units one command carries; the rest of a larger selection is dropped.</summary>
        public const int MaxSubjects = 127;

        public byte Type;
        public bool Queue;
        public int Argument;
        public float3 Position;
        public int Target;
        public int PrefabTypeId;
        public FixedList512Bytes<int> Subjects;
    }
}
