using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Where a hull's rear axle stands on XZ and which way the hull faces.</summary>
    public struct AxlePose
    {
        public float2 Position;
        public float2 Facing;

        public AxlePose(float2 position, float2 facing)
        {
            Position = position;
            Facing = facing;
        }
    }
}
