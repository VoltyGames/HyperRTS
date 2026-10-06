using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Keeps landing pads and their aircraft in step and runs Return to Base orders: the aircraft flies to its pad
    /// (claiming a free one at the ordered airfield first) and docks. Pads free up when their aircraft dies or moves
    /// to another airfield; aircraft whose airfield is destroyed or captured lose their home and stay airborne.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(MoveOrderSystem))]
    public partial struct PadSystem : ISystem
    {
        private BufferLookup<LandingPad> _pads;
        private ComponentLookup<HomePad> _homes;
        private ComponentLookup<Faction> _factions;
        private ComponentLookup<ConstructionProgress> _sites;
        private ComponentLookup<LocalTransform> _transforms;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _pads = state.GetBufferLookup<LandingPad>();
            _homes = state.GetComponentLookup<HomePad>(true);
            _factions = state.GetComponentLookup<Faction>(true);
            _sites = state.GetComponentLookup<ConstructionProgress>(true);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _pads.Update(ref state);
            _homes.Update(ref state);
            _factions.Update(ref state);
            _sites.Update(ref state);
            _transforms.Update(ref state);

            new ReleaseJob { Homes = _homes }.ScheduleParallel();
            var airfields = new Airfields { Pads = _pads, Factions = _factions, Sites = _sites };
            new HomeJob { Airfields = airfields }.Schedule();
            new ReturnJob { Airfields = airfields, Transforms = _transforms }.Schedule();
        }

        /// <summary>Frees pads whose aircraft is gone or now calls another pad home.</summary>
        [BurstCompile]
        private partial struct ReleaseJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<HomePad> Homes;

            private void Execute(Entity entity, DynamicBuffer<LandingPad> pads)
            {
                for (var i = 0; i < pads.Length; i++)
                {
                    ref var pad = ref pads.ElementAt(i);
                    if (pad.Aircraft == Entity.Null)
                    {
                        continue;
                    }

                    var home = Homes.TryGetComponent(pad.Aircraft, out var found) ? found : default;
                    if (home.Airfield != entity || home.Pad != i)
                    {
                        pad.Aircraft = Entity.Null;
                    }
                }
            }
        }

        /// <summary>Single-threaded: claims pads for new arrivals (fresh from production), drops lost homes.</summary>
        [BurstCompile]
        [WithPresent(typeof(Docked), typeof(MoveDestination))]
        private partial struct HomeJob : IJobEntity
        {
            public Airfields Airfields;

            private void Execute(Entity entity, ref HomePad home, EnabledRefRW<Docked> docked,
                EnabledRefRO<MoveDestination> moving, in Faction faction)
            {
                // Any move lifts a docked aircraft off its pad.
                if (docked.ValueRO && moving.ValueRO)
                {
                    docked.ValueRW = false;
                }

                if (home.Airfield == Entity.Null)
                {
                    return;
                }

                if (!Airfields.IsOwn(home.Airfield, faction.Value) || !Airfields.Claim(entity, home))
                {
                    home = default;
                    docked.ValueRW = false;
                }
            }
        }

        /// <summary>Single-threaded: rehoming claims pads other aircraft may want in the same frame.</summary>
        [BurstCompile]
        [WithPresent(typeof(Docked), typeof(MoveDestination))]
        private partial struct ReturnJob : IJobEntity
        {
            public Airfields Airfields;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(Entity entity, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                ref MoveDestination destination, EnabledRefRW<MoveDestination> moving, ref HomePad home,
                EnabledRefRW<Docked> docked, in NavAgent agent, in Faction faction)
            {
                if (order.Value.Type != OrderType.ReturnToBase)
                {
                    return;
                }

                // Ordered to another airfield: move there if it has room, else keep the current home.
                var airfield = order.Value.Target;
                if (airfield != Entity.Null && airfield != home.Airfield)
                {
                    Airfields.TryRehome(entity, ref home, airfield, faction.Value);
                }

                if (home.Airfield == Entity.Null)
                {
                    busy.ValueRW = false;
                    return;
                }

                var pad = Transforms[home.Airfield].TransformPoint(Airfields.Pads[home.Airfield][home.Pad].Offset);
                if (math.distance(Transforms[entity].Position.xz, pad.xz) > agent.Radius)
                {
                    ReachMath.MoveTo(ref destination, moving, pad);
                    return;
                }

                ActiveOrder.Finish(busy, moving);
                docked.ValueRW = true;
            }
        }

        /// <summary>Pad bookkeeping shared by the jobs above.</summary>
        private struct Airfields
        {
            public BufferLookup<LandingPad> Pads;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            [ReadOnly] public ComponentLookup<ConstructionProgress> Sites;

            public bool IsOwn(Entity airfield, byte faction) =>
                AirfieldRules.IsAirfieldOf(Pads, Factions, Sites, airfield, faction);

            /// <summary>Takes the home pad if free; false when another aircraft holds it.</summary>
            public bool Claim(Entity aircraft, in HomePad home)
            {
                var pads = Pads[home.Airfield];
                if (home.Pad < 0 || home.Pad >= pads.Length)
                {
                    return false;
                }

                ref var pad = ref pads.ElementAt(home.Pad);
                if (pad.Aircraft == Entity.Null)
                {
                    pad.Aircraft = aircraft;
                }

                return pad.Aircraft == aircraft;
            }

            /// <summary>Moves the aircraft's home to a free pad of the airfield; the old pad frees next frame.</summary>
            public void TryRehome(Entity aircraft, ref HomePad home, Entity airfield, byte faction)
            {
                if (!IsOwn(airfield, faction))
                {
                    return;
                }

                var pads = Pads[airfield];
                var free = LandingPad.FindFree(pads);
                if (free < 0)
                {
                    return;
                }

                pads.ElementAt(free).Aircraft = aircraft;
                home = new HomePad { Airfield = airfield, Pad = free };
            }
        }
    }
}
