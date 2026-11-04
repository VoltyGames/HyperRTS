using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common.Infantry
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
    [GhostEnabledBit]
    public struct SquadWork : IComponentData, IEnableableComponent
    {
        /// <summary>Metres from the footprint's edge to a digging member.</summary>
        public const float Standoff = 0.9f;

        /// <summary>Furthest a digging member stands from its squad, which halts short of a site by its reach.</summary>
        public const float MaxSpread = 6f;

        /// <summary>Share of the site's length the crew spreads over.</summary>
        private const float Spread = 0.9f;

        /// <summary>World XZ centre of the site, and the unit direction of its length.</summary>
        [GhostField(SendData = false)] public float2 Center;
        [GhostField(SendData = false)] public float2 Along;

        [GhostField(SendData = false)] public float Length;

        /// <summary>Half the site's width.</summary>
        [GhostField(SendData = false)] public float HalfWidth;

        /// <summary>0 to 1: how far the site is built, sent so the HUD can show it.</summary>
        [GhostField(Quantization = 100)] public float Progress;

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
