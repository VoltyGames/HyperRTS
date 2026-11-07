using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// After a section breaks, floods the supports up from the anchored sections and drops what no longer reaches
    /// the ground, one support level every <see cref="SectionedBuilding.FallDelay"/>. A fall breaks the section, takes
    /// its health off the building's and marks its floor fallen in <see cref="IntactFloors"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
    public partial struct SectionCollapseSystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SectionedBuilding>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CollapseJob
            {
                Now = SystemAPI.Time.ElapsedTime,
                Buildings = SystemAPI.GetComponentLookup<SectionedBuilding>(false),
                Sections = SystemAPI.GetBufferLookup<Section>(false),
                Health = SystemAPI.GetComponentLookup<Health>(false),
                Floors = SystemAPI.GetComponentLookup<IntactFloors>(false),
                Repairables = SystemAPI.GetComponentLookup<Repairable>(true),
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
            public ComponentLookup<Health> Health;
            public ComponentLookup<IntactFloors> Floors;
            [ReadOnly] public ComponentLookup<Repairable> Repairables;

            private void Execute(Entity entity)
            {
                var building = Buildings[entity];
                var due = building.Falling != 0 && Now >= building.NextFall;
                if ((building.Broken == building.Checked && !due) || !Common.Health.IsAlive(Health, entity))
                {
                    return;
                }

                var sections = Sections[entity];
                if (building.Broken != building.Checked)
                {
                    Check(ref building, sections);
                }
                else
                {
                    Fall(entity, ref building, sections);
                }

                Buildings[entity] = building;
                if (Floors.TryGetComponent(entity, out var floors))
                {
                    floors.Fallen = building.FallenFloors(sections);
                    Floors[entity] = floors;
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
                var health = Health[entity];
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

                Health[entity] = health;
                building.Fell |= due;
                building.Checked = building.Broken;
                building.NextFall = building.Falling != 0 ? Now + SectionedBuilding.FallDelay : 0;
            }
        }
    }
}
