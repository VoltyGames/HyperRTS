using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ExpireJob().ScheduleParallel();
            var airfields = new Airfields { Pads = SystemAPI.GetBufferLookup<LandingPad>(false), Factions = SystemAPI.GetComponentLookup<Faction>(true), Sites = SystemAPI.GetComponentLookup<ConstructionProgress>(true) };
            new HomeJob { Airfields = airfields }.Schedule();
            new ReturnJob { Airfields = airfields, Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true) }.Schedule();
        }

        /// <summary>Frees pads whose aircraft did not renew them last frame, as when it died or rehomed.</summary>
        [BurstCompile]
        private partial struct ExpireJob : IJobEntity
        {
            private void Execute(DynamicBuffer<LandingPad> pads)
            {
                for (var i = 0; i < pads.Length; i++)
                {
                    pads.ElementAt(i).Reservation.Expire();
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
                    home.Release();
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
                    destination.MoveTo(moving, pad);
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
                AirfieldRules.IsOwnedAirfield(Pads, Factions, Sites, airfield, faction);

            /// <summary>Takes the home pad if free; false when another aircraft holds it.</summary>
            public bool Claim(Entity aircraft, in HomePad home)
            {
                var pads = Pads[home.Airfield];
                if (home.Pad < 0 || home.Pad >= pads.Length)
                {
                    return false;
                }

                return pads.ElementAt(home.Pad).Reservation.TryReserve(aircraft);
            }

            /// <summary>Moves the aircraft's home to a free pad of its kind at the airfield; the old pad frees next frame.</summary>
            public void TryRehome(Entity aircraft, ref HomePad home, Entity airfield, byte faction)
            {
                if (!IsOwn(airfield, faction))
                {
                    return;
                }

                var pads = Pads[airfield];
                var free = LandingPad.FindFree(pads, home.Kind);
                if (free < 0)
                {
                    return;
                }

                pads.ElementAt(free).Reservation.Reserve(aircraft);
                home = home.ToPad(airfield, free);
            }
        }
    }
}
