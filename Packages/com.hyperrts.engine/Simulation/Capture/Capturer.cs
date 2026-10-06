using Unity.Entities;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Captures <see cref="Capturable"/> buildings through Capture orders.</summary>
    public struct Capturer : IComponentData
    {
        public float Rate;
        public bool ConsumedOnCapture;
    }
}
