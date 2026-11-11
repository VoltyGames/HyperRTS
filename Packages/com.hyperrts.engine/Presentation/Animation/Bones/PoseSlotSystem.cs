using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common.Systems;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// Keeps the <see cref="PoseSlots"/> singleton and its GPU copy, the global _PoseSlots buffer LitTeam reads, uploading
    /// the slots written this frame. The cap follows the effect detail level. Not Burst: it owns a graphics buffer.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class PoseSlotSystem : SystemBase
    {
        private static readonly int SlotsId = Shader.PropertyToID("_PoseSlots");

        private GraphicsBuffer _buffer;
        private Entity _slots;

        protected override void OnCreate()
        {
            // A -nographics client draws nothing, so it keeps no slots and no figure becomes a ragdoll.
            if (!GraphicsSupport.Available)
            {
                Enabled = false;
                return;
            }

            var slots = PoseSlots.Create();
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, slots.Skins.Length, UnsafeUtility.SizeOf<float4>());
            _buffer.SetData(slots.Skins);
            _slots = EntityManager.CreateSingleton(slots, "Pose Slots");
        }

        protected override void OnDestroy()
        {
            if (_buffer == null)
            {
                return;
            }

            CompleteDependency();
            EntityManager.GetComponentData<PoseSlots>(_slots).Dispose();
            _buffer.Release();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            ref var slots = ref SystemAPI.GetSingletonRW<PoseSlots>().ValueRW;
            slots.Cap = EffectDetail.RagdollsAt(PresentationQuality.Current.Effects);
            slots.Upload(_buffer);
            Shader.SetGlobalBuffer(SlotsId, _buffer);
        }
    }
}
