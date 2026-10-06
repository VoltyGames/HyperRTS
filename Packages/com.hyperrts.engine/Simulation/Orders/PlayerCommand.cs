using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Kinds of <see cref="PlayerCommand"/>. Games add their own from <see cref="Custom"/> upward.</summary>
    public enum CommandType : byte
    {
        None = 0,

        /// <summary>Right-click: resolved per unit to move, attack, gather or build from the target.</summary>
        Smart = 1,
        Move = 2,
        AttackMove = 3,
        Attack = 4,
        Gather = 5,
        Build = 6,
        Stop = 7,

        /// <summary><see cref="PlayerCommand.Argument"/> is a <c>Stance</c>.</summary>
        SetStance = 8,

        /// <summary>Place <see cref="PlayerCommand.Prefab"/> at Position and send builders to it.</summary>
        PlaceBuilding = 9,

        /// <summary>Queue <see cref="PlayerCommand.Prefab"/> at a producer.</summary>
        Produce = 10,

        /// <summary>Cancel queue slot <see cref="PlayerCommand.Argument"/> (-1 = last) at a producer.</summary>
        CancelProduction = 11,
        SetRallyPoint = 12,

        /// <summary>Sell the commanded or selected buildings for a refund.</summary>
        Sell = 13,

        /// <summary>Builders restore an allied building's health.</summary>
        Repair = 14,

        /// <summary>Take over <see cref="PlayerCommand.Target"/>, a capturable building.</summary>
        Capture = 15,

        /// <summary>Board or garrison <see cref="PlayerCommand.Target"/>.</summary>
        Enter = 16,

        /// <summary>Let passengers out; <see cref="PlayerCommand.Argument"/> is the slot, -1 for all.</summary>
        Unload = 17,

        /// <summary>Unit ability <see cref="PlayerCommand.Argument"/> (its id) at Target or Position.</summary>
        UseAbility = 18,

        /// <summary>Player-level ability (support power) <see cref="PlayerCommand.Argument"/> at Target or Position.</summary>
        UsePower = 19,

        /// <summary>Attack-move back and forth between where each unit stands and Position.</summary>
        Patrol = 20,

        /// <summary>Follow the friendly unit <see cref="PlayerCommand.Target"/> and fight what comes near.</summary>
        Escort = 21,

        /// <summary>Aircraft fly to their pad and dock; an own airfield as Target rehomes them there if a pad is free.</summary>
        ReturnToBase = 22,
        Custom = 128,
    }

    /// <summary>
    /// A player intent, recorded on the player entity by input or AI and consumed the same frame.
    /// This is the only path from input to simulation, so it is also the future network message.
    /// </summary>
    public struct PlayerCommand : IBufferElementData
    {
        public CommandType Type;

        /// <summary>Unit or producer to command; <c>Entity.Null</c> means the player's selected entities.</summary>
        public Entity Unit;

        /// <summary>With <see cref="SubjectCount"/> above 0, the command's own group in the player's
        /// <see cref="PlayerCommandSubject"/> buffer, which overrides <see cref="Unit"/> and the selection.</summary>
        public ushort SubjectStart;

        public ushort SubjectCount;

        public Entity Target;
        public float3 Position;
        public Entity Prefab;

        /// <summary>Shift: append to the order queue instead of replacing it.</summary>
        public bool Queue;

        public int Argument;
    }
}
