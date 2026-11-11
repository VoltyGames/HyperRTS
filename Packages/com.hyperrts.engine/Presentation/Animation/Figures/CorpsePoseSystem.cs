using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Writes each moving ragdoll's pose into its slot: every bone moves with the body it rides on since the handover.
    /// It also keeps the copies' render bounds round the bodies and marks a corpse slow once every body is at rest.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CorpseSystem))]
    [UpdateBefore(typeof(PoseSlotSystem))]
    public partial struct CorpsePoseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PoseSlots>();
            state.RequireForUpdate<Corpse>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new PoseJob
            {
                Now = (float)SystemAPI.Time.ElapsedTime,
                Slots = SystemAPI.GetSingletonRW<PoseSlots>().ValueRO,
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                Bounds = SystemAPI.GetComponentLookup<RenderBounds>(),
            }.Schedule(state.Dependency);
        }

        // Single-threaded: every corpse writes into the one slot array.
        [BurstCompile]
        private partial struct PoseJob : IJobEntity
        {
            public float Now;
            public PoseSlots Slots;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            public ComponentLookup<RenderBounds> Bounds;

            private void Execute(ref Corpse corpse, in DynamicBuffer<CorpseBody> bodies, in DynamicBuffer<CorpseBone> bones,
                in DynamicBuffer<LinkedEntityGroup> copies)
            {
                if (bodies.Length == 0)
                {
                    return;
                }

                ref var ragdoll = ref corpse.Clips.Value.Ragdoll;
                var toObject = math.inverse(corpse.World);
                var moves = new NativeArray<float4x4>(bodies.Length, Allocator.Temp);
                var low = new float3(float.MaxValue);
                var high = new float3(float.MinValue);
                var slow = true;
                for (var b = 0; b < bodies.Length; b++)
                {
                    var place = Transforms[bodies[b].Body];
                    moves[b] = math.mul(toObject, math.mul(float4x4.TRS(place.Position, place.Rotation, 1f), bodies[b].Start));
                    var at = math.transform(toObject, place.Position);
                    low = math.min(low, at);
                    high = math.max(high, at);
                    slow &= IsSlow(Velocities[bodies[b].Body], ragdoll.Tuning.restSpeed);
                }

                for (var bone = 0; bone < bones.Length; bone++)
                {
                    Slots.Write(corpse.Slot, bone, math.mul(moves[ragdoll.BoneBodies[bone]], bones[bone].Handover));
                }

                var box = new RenderBounds { Value = new AABB { Center = (low + high) * 0.5f, Extents = (high - low) * 0.5f + corpse.Reach } };
                for (var i = 1; i < copies.Length; i++)
                {
                    Bounds[copies[i].Value] = box;
                }

                corpse.SlowSince = slow && !corpse.Slow ? Now : corpse.SlowSince;
                corpse.Slow = slow;
            }

            private static bool IsSlow(in PhysicsVelocity velocity, float restSpeed) =>
                math.length(velocity.Linear) < restSpeed && math.length(velocity.Angular) < restSpeed;
        }
    }
}
