using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A shot that landed this frame, for presentation only (craters, scorch marks): nothing in the simulation reads
    /// it. It names the shooter by type, so a client or a replay finds the marks through its own copy of the prefab.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ImpactEvent : IBufferElementData
    {
        /// <summary><c>EntityInfo.TypeId</c> of the shooter whose <see cref="EntityImpactMark"/>s apply.</summary>
        public int TypeId;

        public float3 Position;

        /// <summary>The hit's splash radius; bigger blasts leave bigger marks.</summary>
        public float Radius;

        /// <summary>Owner of the shooter; decides who sees the impact through fog.</summary>
        public byte Faction;

        public ImpactSurface Surface;

        /// <summary>What was struck: the target's material, water, or the <see cref="GroundMaterials"/> cell under open ground.</summary>
        public SurfaceMaterial Material;

        /// <summary>What the shot was aimed at, for <see cref="ImpactSurfaceSystem"/>; it never leaves the frame.</summary>
        public Entity Target;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="ImpactEvent"/>s.</summary>
    public struct ImpactQueue : IComponentData { }
}
