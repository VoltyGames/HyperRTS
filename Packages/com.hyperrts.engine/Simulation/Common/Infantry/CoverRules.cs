using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>On the match: what each kind of cover takes off a hit, from which side, and how far a halted squad looks for it.</summary>
    public struct CoverRules : IComponentData
    {
        /// <summary>The number of <see cref="CoverKind"/> values, so <see cref="Entries"/> has one for each.</summary>
        public const int Kinds = 3;

        /// <summary>One entry per <see cref="CoverKind"/>, indexed by it.</summary>
        public FixedList64Bytes<CoverEntry> Entries;

        /// <summary>Cosine of the half angle of the front arc a spot protects.</summary>
        public float ArcCos;

        /// <summary>Metres round a halted squad to look for spots, at most its spread.</summary>
        public float SearchRadius;

        /// <summary>Metres from a halted squad's place that it looks for spots, given the squad's furthest spread.</summary>
        public readonly float Reach(float maxSpread) => math.min(SearchRadius, maxSpread);

        public readonly CoverEntry Entry(CoverKind kind) => Entries[(int)kind];

        /// <summary>Whether a spot facing <paramref name="spotFacing"/> protects a squad facing <paramref name="forward"/>.</summary>
        public readonly bool Faces(float2 spotFacing, float2 forward) => math.dot(spotFacing, forward) >= ArcCos;
    }
}
