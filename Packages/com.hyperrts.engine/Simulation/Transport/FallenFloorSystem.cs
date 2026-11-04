using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Each standing floor of a container with <see cref="IntactFloors"/> holds an even share of its slots, filled
    /// ground floor first in boarding order. When a floor falls its passengers take a hit of
    /// <see cref="FallHit"/> of their maximum health and get out, and its slots stay shut
    /// (<see cref="Container.LostSlots"/>) until it is rebuilt.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(ContainerDeathSystem))]
    public partial struct FallenFloorSystem : ISystem
    {
        /// <summary>Share of a passenger's maximum health a falling floor takes.</summary>
        public const float FallHit = 0.6f;

        private CargoExit _exit;
        private EventWriter<DamageEvent> _damage;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _exit = new CargoExit(ref state);
            _damage = new EventWriter<DamageEvent>(ref state);
            state.RequireForUpdate<IntactFloors>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _exit.Update(ref state);
            _damage.Update(ref state);
            new EmptyJob { Exit = _exit, Damage = _damage, Grid = SystemAPI.GetSingleton<NavGrid>() }.Schedule();
        }

        /// <summary>Single-threaded: exits and hits write other entities.</summary>
        [BurstCompile]
        private partial struct EmptyJob : IJobEntity
        {
            public CargoExit Exit;
            public EventWriter<DamageEvent> Damage;
            [ReadOnly] public NavGrid Grid;

            private void Execute(Entity entity, in Container container, ref IntactFloors floors,
                DynamicBuffer<Cargo> cargo)
            {
                if (floors.Fallen == floors.Emptied)
                {
                    return;
                }

                var per = container.PerFloor(floors.Count);
                var leaving = Leaving(cargo, floors, per);
                var attacker = Exit.Attacker(entity);
                for (var i = cargo.Length - 1; i >= 0; i--)
                {
                    if ((leaving & (1ul << i)) == 0)
                    {
                        continue;
                    }

                    var unit = cargo[i].Unit;
                    Damage.Add(DamageEvent.Direct(unit, Exit.Position(entity), attacker.Source, attacker.Faction,
                        FallHit * Exit.MaxHealth(unit)));
                    Exit.Unload(entity, cargo, i, Grid);
                }

                floors.Emptied = floors.Fallen;
            }

            /// <summary>Passengers, a bit each, on floors that fell since the last emptying.</summary>
            private static ulong Leaving(DynamicBuffer<Cargo> cargo, in IntactFloors floors, int per)
            {
                var fell = floors.Fallen & ~floors.Emptied;
                var leaving = 0ul;
                var used = 0;
                for (var i = 0; i < cargo.Length && i < 64; i++)
                {
                    if ((fell & (1 << Standing(floors, used / per))) != 0)
                    {
                        leaving |= 1ul << i;
                    }

                    used += cargo[i].Size;
                }

                return leaving;
            }

            /// <summary>The <paramref name="index"/>th floor that stood before this fall, else the top one.</summary>
            private static int Standing(in IntactFloors floors, int index)
            {
                var top = 0;
                for (var floor = 0; floor < floors.Count; floor++)
                {
                    if ((floors.Emptied & (1 << floor)) != 0)
                    {
                        continue;
                    }

                    if (index-- == 0)
                    {
                        return floor;
                    }

                    top = floor;
                }

                return top;
            }
        }
    }
}
