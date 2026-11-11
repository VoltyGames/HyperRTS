using Unity.Entities;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>How wet the ground is after rain (0 dry, 1 soaked), as a singleton the <see cref="Atmosphere"/> writes; absent means dry.</summary>
    public struct GroundWetness : IComponentData
    {
        public float Level;
    }
}
