using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>What a squad member does where it stands; clients play the clip for it.</summary>
    public enum MemberAction : byte
    {
        None,
        Dig,
    }

    /// <summary>
    /// Enabled while a squad raises a building in reach of it: the members spread along the site's long side and dig.
    /// The construction system writes it each frame and the member system reads it.
    /// </summary>
    public struct SquadWork : IComponentData, IEnableableComponent
    {
        /// <summary>Metres from the footprint's edge to a digging member.</summary>
        public const float Standoff = 0.9f;

        /// <summary>Furthest a digging member stands from its squad, which halts short of a site by its reach.</summary>
        public const float MaxSpread = 6f;

        /// <summary>Share of the site's length the crew spreads over.</summary>
        private const float Spread = 0.9f;

        /// <summary>World XZ centre of the site, and the unit direction of its length.</summary>
        public float2 Center;
        public float2 Along;

        public float Length;

        /// <summary>Half the site's width.</summary>
        public float HalfWidth;

        /// <summary>The world XZ position of member <paramref name="rank"/> of <paramref name="count"/>, on the site's side nearer <paramref name="squad"/>.</summary>
        public readonly float2 Position(int rank, int count, float2 squad)
        {
            var side = new float2(-Along.y, Along.x);
            var sign = math.dot(squad - Center, side) >= 0f ? 1f : -1f;
            var along = ((rank + 0.5f) / count - 0.5f) * Length * Spread;
            return Center + Along * along + side * (sign * (HalfWidth + Standoff));
        }
    }
}
