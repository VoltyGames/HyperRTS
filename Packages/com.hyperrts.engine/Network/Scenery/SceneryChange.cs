using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Network.Scenery
{
    /// <summary>One piece of map scenery's state on the wire, named by its <c>SceneryId</c>.</summary>
    public struct SceneryChange
    {
        public const byte Destroyed = 0;
        public const byte Intact = byte.MaxValue;

        /// <summary><see cref="Code"/> of a piece that stands unhurt.</summary>
        public const ushort IntactCode = Intact;

        private const ushort ClearedBit = 1 << 8;

        public ulong Id;

        /// <summary><see cref="Destroyed"/>, else health in 255ths, never below 1 while it stands.</summary>
        public byte State;

        /// <summary>Fall heading (<see cref="ToppleMath"/>), read once it is destroyed.</summary>
        public byte Fall;

        /// <summary>Removed with nothing left, as a building site does: the piece if it stands, else its remains.</summary>
        public bool Cleared;

        public readonly float Fraction => State / (float)Intact;

        /// <summary>State and <see cref="Cleared"/> as one number, the way the tracker compares what teams know.</summary>
        public readonly ushort Code => (ushort)(State | (Cleared ? ClearedBit : 0));

        public static SceneryChange FromCode(ulong id, ushort code, byte fall) =>
            new() { Id = id, State = (byte)code, Fall = fall, Cleared = code >= ClearedBit };

        /// <summary>Rounded up so a sliver of health doesn't read as destroyed.</summary>
        public static byte Encode(in Health health)
        {
            if (health.Current <= 0f)
            {
                return Destroyed;
            }

            return (byte)math.clamp((int)math.ceil(health.Fraction * Intact), 1, Intact);
        }
    }
}
