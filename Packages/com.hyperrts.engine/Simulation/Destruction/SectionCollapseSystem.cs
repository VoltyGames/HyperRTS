using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// After a section breaks, drops the sections no longer connected to the ground, one support level every
    /// <see cref="SectionedBuilding.FallDelay"/>. A building whose last section broke is <see cref="Dying"/> with its
    /// death cue played, and dies once its pieces have fallen.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
    public partial struct SectionCollapseSystem : ISystem
    {
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<SectionedBuilding>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state);
            new CollapseJob
            {
                Cues = _cues,
                Dying = SystemAPI.GetComponentLookup<Dying>(false),
                Now = SystemAPI.Time.ElapsedTime,
                Buildings = SystemAPI.GetComponentLookup<SectionedBuilding>(false),
                Sections = SystemAPI.GetBufferLookup<Section>(false),
                Healths = SystemAPI.GetComponentLookup<Health>(false),
                Floors = SystemAPI.GetComponentLookup<IntactFloors>(false),
                Repairables = SystemAPI.GetComponentLookup<Repairable>(true),
                Clips = SystemAPI.GetComponentLookup<SectionClips>(true),
            }.Schedule();
        }

        /// <summary>Writes through lookups only on a change, so standing buildings keep their change versions.</summary>
        [BurstCompile]
        [WithAll(typeof(SectionedBuilding))]
        private partial struct CollapseJob : IJobEntity
        {
            public double Now;
            public ComponentLookup<SectionedBuilding> Buildings;
            public BufferLookup<Section> Sections;
            public ComponentLookup<Health> Healths;
            public ComponentLookup<IntactFloors> Floors;
            [ReadOnly] public ComponentLookup<Repairable> Repairables;
            [ReadOnly] public ComponentLookup<SectionClips> Clips;
            public ComponentLookup<Dying> Dying;
            public CueWriter Cues;

            private void Execute(Entity entity, in LocalTransform transform, in Owner owner)
            {
                var building = Buildings[entity];
                if (building.DiesAt != 0)
                {
                    Linger(entity, ref building);
                    return;
                }

                var due = building.Falling != 0 && Now >= building.NextFall;
                if (building.Broken == building.Checked && !due)
                {
                    return;
                }

                // Killed outright it dies as it stands; with every section broken the last hit only emptied its health.
                var sections = Sections[entity];
                var standing = SectionedBuilding.All(sections.Length) & ~building.Broken;
                if (standing != 0 && !Health.IsAlive(Healths, entity))
                {
                    return;
                }

                Advance(entity, ref building, sections);
                if (building.DiesAt != 0)
                {
                    Dying.SetComponentEnabled(entity, true);
                    Cues.Play(entity, CueSlot.Death, transform, transform.Forward(), owner.Value);
                }

                Buildings[entity] = building;
                if (Floors.TryGetComponent(entity, out var floors))
                {
                    floors.Fallen = building.FallenFloors(sections);
                    Floors[entity] = floors;
                }
            }

            /// <summary>Checks the supports after a break, else drops the level that is due, and ends a building with no section left.</summary>
            private void Advance(Entity entity, ref SectionedBuilding building, DynamicBuffer<Section> sections)
            {
                if (building.Broken != building.Checked)
                {
                    Check(ref building, sections);
                }
                else
                {
                    Fall(entity, ref building, sections);
                }

                if ((SectionedBuilding.All(sections.Length) & ~building.Broken) == 0 && !Repairables.HasComponent(entity))
                {
                    Finish(entity, ref building);
                }
            }

            /// <summary>
            /// Ends a building with no section left: at once, or after its break clips when its pieces play them, so
            /// the ruin does not replace pieces still in the air. Section shares can sum to a hair under the
            /// building's health, so the health is set either way.
            /// </summary>
            private void Finish(Entity entity, ref SectionedBuilding building)
            {
                var falls = Clips.TryGetComponent(entity, out var clips) && clips.IsSet;
                building.DiesAt = falls ? Now + clips.Longest : 0;
                var health = Healths[entity];
                health.Current = falls ? SectionedBuilding.LingeringHealth : 0f;
                Healths[entity] = health;
            }

            /// <summary>Keeps a building alive through hits until <see cref="SectionedBuilding.DiesAt"/>, unless a repair stood a section up again.</summary>
            private void Linger(Entity entity, ref SectionedBuilding building)
            {
                if ((SectionedBuilding.All(Sections[entity].Length) & ~building.Broken) != 0)
                {
                    building.DiesAt = 0;
                    Buildings[entity] = building;
                    Dying.SetComponentEnabled(entity, false);
                    return;
                }

                var health = Healths[entity];
                var wanted = Now >= building.DiesAt ? 0f : SectionedBuilding.LingeringHealth;
                if (health.Current != wanted)
                {
                    health.Current = wanted;
                    Healths[entity] = health;
                }
            }

            /// <summary>Marks what lost its way to the ground; the first level falls a delay later.</summary>
            private void Check(ref SectionedBuilding building, DynamicBuffer<Section> sections)
            {
                building.Checked = building.Broken;
                var standing = SectionedBuilding.All(sections.Length) & ~building.Broken;
                building.Falling = standing & ~SectionedBuilding.Supported(sections);
                if (building.Falling == 0)
                {
                    building.NextFall = 0;
                }
                else if (building.NextFall == 0)
                {
                    building.NextFall = Now + SectionedBuilding.FallDelay;
                }
            }

            private void Fall(Entity entity, ref SectionedBuilding building, DynamicBuffer<Section> sections)
            {
                var due = building.DueToFall(sections);
                var health = Healths[entity];
                for (var i = 0; i < sections.Length; i++)
                {
                    if ((due & (1ul << i)) != 0)
                    {
                        health.Current = math.max(0f, health.Current - building.Damage(sections, i, float.MaxValue));
                    }
                }

                if (Repairables.HasComponent(entity))
                {
                    health.Current = Repairable.Health(sections);
                }

                Healths[entity] = health;
                building.Fell |= due;
                building.Checked = building.Broken;
                building.NextFall = building.Falling != 0 ? Now + SectionedBuilding.FallDelay : 0;
            }
        }
    }
}
