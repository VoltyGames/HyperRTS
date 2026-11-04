using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>On the match: what cover takes off a hit, from which side, and how far a halted squad looks for it.</summary>
    public struct CoverRules : IComponentData
    {
        public float HeavyDamage;
        public float HeavySuppression;
        public float LightDamage;
        public float LightSuppression;

        /// <summary>Cosine of the half angle of the front arc a spot protects.</summary>
        public float ArcCos;

        /// <summary>Metres round a halted squad to look for spots, at most <see cref="SquadMember.MaxSpread"/>.</summary>
        public float SearchRadius;

        /// <summary>Metres from a halted squad's place that it looks for spots.</summary>
        public readonly float Reach => math.min(SearchRadius, SquadMember.MaxSpread);

        /// <summary>Whether a spot facing <paramref name="spotFacing"/> protects a squad facing <paramref name="forward"/>.</summary>
        public readonly bool Faces(float2 spotFacing, float2 forward) => math.dot(spotFacing, forward) >= ArcCos;

        /// <summary>Share of damage a member in <paramref name="kind"/> cover takes from the front.</summary>
        public readonly float DamageTaken(CoverKind kind)
        {
            if (kind == CoverKind.Heavy)
            {
                return HeavyDamage;
            }

            return kind == CoverKind.Light ? LightDamage : 1f;
        }

        /// <summary>Share of suppression a member in <paramref name="kind"/> cover takes from the front.</summary>
        public readonly float SuppressionTaken(CoverKind kind)
        {
            if (kind == CoverKind.Heavy)
            {
                return HeavySuppression;
            }

            return kind == CoverKind.Light ? LightSuppression : 1f;
        }
    }
}
