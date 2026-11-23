using Unity.Entities;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>A moving part (a turret, a door) that hides and shows with the intact model of section <see cref="Section"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionPart : IBufferElementData
    {
        /// <summary>Index of the section it belongs to.</summary>
        public byte Section;

        public Entity Part;
    }
}
