using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>What a unit, building or tree is made of, for the effects of shots that hit it.</summary>
    public struct HitMaterial : IComponentData
    {
        public SurfaceMaterial Value;
    }

    /// <summary>What the entity's shots throw up where they land, by the material struck.</summary>
    public struct EntityImpactEffects : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;
    }

    /// <summary>Dust a vehicle raises as it moves, by the ground material under it and its speed.</summary>
    public struct MotionDust : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;

        /// <summary>Socket position in the entity's local space (behind the rear wheels or tracks).</summary>
        public float3 Socket;

        /// <summary>Speed in m/s at which the dust streams at full rate.</summary>
        public float FullSpeed;

        /// <summary>Below this many m/s a vehicle creeping or jostled in a crowd raises none.</summary>
        public float MinSpeed;

        /// <summary>Stream rate from 0 to 1 for the vehicle's driven speed, either way along its facing.</summary>
        public readonly float Strength(float speed) =>
            math.saturate((math.abs(speed) - MinSpeed) / math.max(FullSpeed - MinSpeed, 0.01f));
    }

    /// <summary>Dust a rotorcraft's downwash raises from the ground under it, by the ground material and its height.</summary>
    public struct RotorWash : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;

        /// <summary>Metres above the ground from which no dust rises; full dust at the ground.</summary>
        public float Height;

        /// <summary>The authored default, also the downwash reach of rotors with no wash.</summary>
        public const float DefaultHeight = 25f;
    }
}
