using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Plays the Ready cue for every unit produced and building completed this frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup), OrderLast = true)]
    public partial struct ReadyCueSystem : ISystem
    {
        private CueWriter _cues;
        private ComponentLookup<LocalTransform> _transforms;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            _transforms.Update(ref state);
            new PlayJob { Cues = _cues, Transforms = _transforms }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct PlayJob : IJobEntity
        {
            public CueWriter Cues;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(in DynamicBuffer<UnitProduced> units, in DynamicBuffer<BuildingCompleted> buildings)
            {
                foreach (var produced in units)
                {
                    var spawn = LocalTransform.FromPosition(produced.Position);
                    Cues.Play(produced.Prefab, CueSlot.Ready, spawn, spawn.Forward(), produced.Faction);
                }

                foreach (var completed in buildings)
                {
                    var building = Transforms[completed.Building];
                    Cues.Play(completed.Building, CueSlot.Ready, building, building.Forward(), completed.Faction);
                }
            }
        }
    }
}
