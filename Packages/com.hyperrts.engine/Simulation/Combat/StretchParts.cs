using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Parts of a prop still at rest pose, stretched by <see cref="StretchPartsSystem"/> once it gets a
    /// <see cref="PartStretch"/> (remains of a stretched prop).
    /// </summary>
    public struct StretchParts : IComponentData
    {
        public Entity StartCap;
        public Entity EndCap;
        public Entity Span;
        public StretchLayout Layout;
    }
}
