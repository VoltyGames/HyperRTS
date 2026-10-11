using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>Plays the Ready cue for every unit produced and building completed this frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup), OrderLast = true)]
    public partial struct ReadySoundSystem : ISystem
    {
        private SoundWriter _sounds;
        private ComponentLookup<LocalTransform> _transforms;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sounds = new SoundWriter(ref state);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<SoundQueue>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());
            _transforms.Update(ref state);
            new PlayJob { Sounds = _sounds, Transforms = _transforms }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct PlayJob : IJobEntity
        {
            public SoundWriter Sounds;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(in DynamicBuffer<UnitProduced> units, in DynamicBuffer<BuildingCompleted> buildings)
            {
                foreach (var produced in units)
                {
                    Sounds.Play(produced.Prefab, SoundSlot.Ready, produced.Position, produced.Faction);
                }

                foreach (var completed in buildings)
                {
                    var position = Transforms[completed.Building].Position;
                    Sounds.Play(completed.Building, SoundSlot.Ready, position, completed.Faction);
                }
            }
        }
    }
}
