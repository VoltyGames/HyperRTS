using Unity.Collections;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>What makes a squad figure show its other model.</summary>
    public enum VariantReason : byte
    {
        /// <summary>Its member fires the upgraded weapon.</summary>
        Upgraded,

        /// <summary>Its squad's weapon is set up and the figure takes a crew place.</summary>
        Emplaced,
    }

    /// <summary>A figure model a squad figure can show instead of its own, with what a <see cref="SquadFigure"/> keeps for the model shown.</summary>
    public struct FigureVariant
    {
        /// <summary>Why the variant shows; the base model shows the rest of the time.</summary>
        public VariantReason Reason;
        public Entity Figure;
        public FixedString32Bytes Weapon;
        public FigureClips Clips;
        public float Height;
    }

    /// <summary>Where a figure stands and how it holds itself while its squad's weapon is set up, such as a gunner prone behind the gun.</summary>
    public struct CrewPlace
    {
        /// <summary>False leaves the figure at its member's slot.</summary>
        public bool Takes;

        /// <summary>Metres in the unit's frame: x to the right, y ahead.</summary>
        public float2 Offset;

        public Posture Posture;
    }
}
