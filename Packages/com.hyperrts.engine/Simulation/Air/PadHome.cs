using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>The airfield and pad an aircraft returns to; <c>Entity.Null</c> while it has no home.</summary>
    public struct PadHome : IComponentData
    {
        public Entity Airfield;
        public int Pad;
    }
}
