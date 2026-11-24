using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Destruction;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Silences the weapon mounts of broken sections, and frees them again once the section is repaired.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(TargetingSystemGroup))]
    public partial struct SectionWeaponSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SectionWeapon>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new SilenceJob().Schedule();

        // A change filter, so only a building whose broken mask was written is visited.
        [BurstCompile]
        [WithChangeFilter(typeof(SectionedBuilding))]
        private partial struct SilenceJob : IJobEntity
        {
            private void Execute(in SectionedBuilding building, in DynamicBuffer<SectionWeapon> weapons,
                DynamicBuffer<WeaponMount> mounts) => SectionWeapon.Apply(weapons, building.Broken, mounts);
        }
    }
}
