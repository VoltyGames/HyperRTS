using Unity.Entities;

namespace HyperRTS.Presentation.Environment.Trampling
{
    /// <summary>A building that shows as a ditch in the terrain once built; see <see cref="TrenchAuthoring"/>.</summary>
    public struct Trench : IComponentData
    {
        /// <summary>Metres dug.</summary>
        public float Depth;
    }
}
