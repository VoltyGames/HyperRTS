using System;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>A live figure past its handover, waiting to become a ragdoll.</summary>
    internal struct CorpseHandover : IComparable<CorpseHandover>
    {
        public Entity Squad;
        public int Figure;
        public SquadMember Member;
        public float4x4 World;
        public float Distance;

        public readonly int CompareTo(CorpseHandover other) => Distance.CompareTo(other.Distance);
    }
}
