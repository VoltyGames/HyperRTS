using Unity.Entities;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Written by input, read by the fog renderer: whether the selected units' sight is outlined.</summary>
    public struct SightShapeView : IComponentData
    {
        public bool Shown;
    }
}
