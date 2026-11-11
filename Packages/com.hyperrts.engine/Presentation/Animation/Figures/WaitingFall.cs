using System;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>A figure of a gone squad waiting for the frame's budget to copy it as a corpse.</summary>
    internal struct WaitingFall : IComparable<WaitingFall>
    {
        public ShownFigure Shown;
        public Entity Prefab;
        public SquadFigure Baked;
        public float DieHold;
        public byte Owner;
        public float Since;
        public float Distance;

        public readonly int CompareTo(WaitingFall other) => Distance.CompareTo(other.Distance);
    }
}
