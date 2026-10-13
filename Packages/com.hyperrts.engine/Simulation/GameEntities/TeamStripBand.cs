using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// The height band (object-space metres) where an entity shows its owner's colour, for models painted in team-colour
    /// strips rather than a team mask. Presentation copies it onto each render part.
    /// </summary>
    public struct TeamStripBand : IComponentData
    {
        public float Bottom;
        public float Top;
    }
}
