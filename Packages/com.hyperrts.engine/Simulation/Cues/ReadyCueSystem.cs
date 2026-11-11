using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Plays the Ready cue for every unit produced and building completed this frame, and the producer's
    /// ProductionDone cue for every unit or research it finished.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup), OrderLast = true)]
    public partial struct ReadyCueSystem : ISystem
    {
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state);
            new PlayJob { Cues = _cues, Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true) }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct PlayJob : IJobEntity
        {
            public CueWriter Cues;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(in DynamicBuffer<UnitProduced> units, in DynamicBuffer<BuildingCompleted> buildings,
                in DynamicBuffer<ResearchCompleted> researched)
            {
                foreach (var produced in units)
                {
                    var spawn = LocalTransform.FromPosition(produced.Position);
                    Cues.Play(produced.Prefab, CueSlot.Ready, spawn, spawn.Forward(), produced.Owner);
                    Done(produced.Producer, produced.Owner);
                }

                foreach (var research in researched)
                {
                    Done(research.Producer, research.Owner);
                }

                foreach (var completed in buildings)
                {
                    var building = Transforms[completed.Building];
                    Cues.Play(completed.Building, CueSlot.Ready, building, building.Forward(), completed.Owner);
                }
            }

            private void Done(Entity producer, byte owner)
            {
                var place = Transforms[producer];
                Cues.Play(producer, CueSlot.ProductionDone, place, place.Forward(), owner);
            }
        }
    }
}
