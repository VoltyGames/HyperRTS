using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Scenery that falls away from its lethal hit, such as a tree.</summary>
    public struct Topple : IComponentData
    {
        /// <summary>Unit trunk direction in the standing model, base to top.</summary>
        public float3 Trunk;

        /// <summary>Metres from the stump to the top in the standing model, which sets how fast it falls.</summary>
        public float Height;

        /// <summary>Heading in radians from +Z toward +X that the canopy points in the remains model.</summary>
        public float Lie;

        /// <summary>Metres from the stump to the canopy's widest reach on XZ in the standing model.</summary>
        public float CanopyRadius;
    }

    /// <summary>Remains of a <see cref="Topple"/> piece that presentation shows falling. Their transform already holds the final pose.</summary>
    public struct ToppleFall : IComponentData
    {
        /// <summary>Turns the final canopy direction up to where the trunk stood.</summary>
        public quaternion Tilt;

        /// <summary>Unit world direction the canopy lies toward, where the fall lands.</summary>
        public float3 CanopyDirection;

        /// <summary>Metres from the stump to the top at the remains' scale.</summary>
        public float Height;

        /// <summary>Unwitnessed falls show lying down at once.</summary>
        public bool Witnessed;
    }

    /// <summary>The fall heading (<see cref="ToppleMath"/>) a <see cref="Topple"/> piece's lethal hit gave it.</summary>
    public struct ToppleHeading : IComponentData
    {
        public byte Heading;
    }
}
