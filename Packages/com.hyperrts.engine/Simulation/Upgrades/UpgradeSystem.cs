using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>
    /// Gives every owned entity the stat modifiers of its owner's researched upgrades, catching up new spawns and
    /// swapping the set when ownership changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    public partial struct UpgradeSystem : ISystem
    {
        private EntityQuery _players;
        private BufferLookup<ResearchedUpgrade> _researched;
        private BufferLookup<UpgradeEffect> _effects;
        private BufferLookup<StatModifier> _modifiers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _researched = state.GetBufferLookup<ResearchedUpgrade>(true);
            _effects = state.GetBufferLookup<UpgradeEffect>(true);
            _modifiers = state.GetBufferLookup<StatModifier>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Until someone finishes research there is nothing to apply or strip.
            if (!AnyResearched(ref state))
            {
                return;
            }

            _researched.Update(ref state);
            _effects.Update(ref state);
            _modifiers.Update(ref state);

            new ApplyJob
            {
                PlayerByFaction = PlayerLookup.ByFaction(_players, state.WorldUpdateAllocator),
                Researched = _researched,
                Effects = _effects,
                Modifiers = _modifiers,
            }.ScheduleParallel();
        }

        private bool AnyResearched(ref SystemState state)
        {
            foreach (var researched in SystemAPI.Query<DynamicBuffer<ResearchedUpgrade>>())
            {
                if (!researched.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        [BurstCompile]
        private partial struct ApplyJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            [ReadOnly] public BufferLookup<ResearchedUpgrade> Researched;
            [ReadOnly] public BufferLookup<UpgradeEffect> Effects;

            // Each entity writes only its own buffer, and only when its set changes, so StatSystem's filter stays quiet.
            [NativeDisableParallelForRestriction] public BufferLookup<StatModifier> Modifiers;

            private void Execute(Entity entity, ref AppliedUpgrades applied, in EntityInfo info, in Faction faction)
            {
                if (applied.Faction != faction.Value)
                {
                    Strip(entity, applied);
                    applied = new AppliedUpgrades { Faction = faction.Value };
                }

                if (!Researched.TryGetBuffer(PlayerByFaction[faction.Value], out var researched) ||
                    applied.Count >= researched.Length || !Modifiers.HasBuffer(entity))
                {
                    return;
                }

                var modifiers = Modifiers[entity];
                for (var i = applied.Count; i < researched.Length; i++)
                {
                    foreach (var effect in Effects[researched[i].Upgrade])
                    {
                        if (effect.AppliesTo == 0 || effect.AppliesTo == info.TypeId)
                        {
                            modifiers.Add(effect.Modifier);
                        }
                    }
                }

                applied.Count = researched.Length;
            }

            private void Strip(Entity entity, in AppliedUpgrades applied)
            {
                if (applied.Count == 0 || !Modifiers.HasBuffer(entity) ||
                    !Researched.TryGetBuffer(PlayerByFaction[applied.Faction], out var researched))
                {
                    return;
                }

                var modifiers = Modifiers[entity];
                for (var i = 0; i < applied.Count && i < researched.Length; i++)
                {
                    StatMath.RemoveSource(modifiers, researched[i].TypeId);
                }
            }
        }
    }
}
