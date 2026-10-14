using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>Dust a vehicle raises as it moves, by the ground material under it and its speed.</summary>
    public struct MotionDust : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;

        /// <summary>Socket position in the entity's local space (behind the rear wheels or tracks).</summary>
        public float3 Socket;

        /// <summary>Speed in m/s at which the dust streams at full rate.</summary>
        public float FullSpeed;
    }
}
