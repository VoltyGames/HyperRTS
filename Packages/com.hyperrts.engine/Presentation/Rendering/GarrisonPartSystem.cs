using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Draws each <see cref="GarrisonPart"/> only while its container carries one of its passenger types.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct GarrisonPartSystem : ISystem
    {
        private RenderSwitch _switch;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            state.RequireForUpdate<GarrisonPart>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _switch.Update(ref state);
            var cargo = SystemAPI.GetBufferLookup<Cargo>(true);
            var info = SystemAPI.GetComponentLookup<EntityInfo>(true);
            foreach (var (part, shownFor, entity) in SystemAPI
                         .Query<RefRO<GarrisonPart>, DynamicBuffer<GarrisonPartPassenger>>().WithEntityAccess())
            {
                var hide = !Carries(cargo, info, part.ValueRO.Container, shownFor);
                if (_switch.IsHidden(entity, HideReason.Garrison) != hide)
                {
                    _switch.SetGroup(entity, HideReason.Garrison, hide);
                }
            }
        }

        private static bool Carries(in BufferLookup<Cargo> cargo, in ComponentLookup<EntityInfo> info, Entity container,
            DynamicBuffer<GarrisonPartPassenger> shownFor)
        {
            if (!cargo.TryGetBuffer(container, out var aboard))
            {
                return false;
            }

            foreach (var passenger in aboard)
            {
                if (!info.TryGetComponent(passenger.Unit, out var unit))
                {
                    continue;
                }

                foreach (var type in shownFor)
                {
                    if (type.TypeId == unit.TypeId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
