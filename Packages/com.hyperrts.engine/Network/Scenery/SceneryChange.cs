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

        public ulong Id;

        /// <summary><see cref="Destroyed"/>, else health as a fraction of 255, never below 1 while it stands.</summary>
        public byte State;

        /// <summary>Heading a toppling piece fell toward (<see cref="ToppleMath"/>); read once it is destroyed.</summary>
        public byte Fall;

        public readonly float Fraction => State / (float)Intact;

        /// <summary>Rounded up, so a sliver of health doesn't read as destroyed.</summary>
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
