using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

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
        public float3 End;
        public byte Turn;
        public Entity Target;

        /// <summary>A map scenery target's <c>SceneryId</c>: scenery isn't a ghost, so <see cref="Target"/> can't carry it.</summary>
        public ulong TargetScenery;

        public int PrefabTypeId;

        [GhostFixedListCapacity(Capacity = MaxSubjects)]
        public FixedList4096Bytes<Entity> Subjects;

        /// <summary>The plain fields of <paramref name="command"/>; the sender resolves entities and the prefab.</summary>
        public static CommandRpc From(in PlayerCommand command) => new()
        {
            Type = (byte)command.Type,
            Queue = command.Queue,
            Argument = command.Argument,
            Position = command.Position,
            End = command.End,
            Turn = command.Turn,
        };

        /// <summary>The plain fields back as a command; the receiver resolves entities and the prefab.</summary>
        public readonly PlayerCommand ToCommand() => new()
        {
            Type = (CommandType)Type,
            Queue = Queue,
            Argument = Argument,
            Position = Position,
            End = End,
            Turn = Turn,
        };
    }
}
