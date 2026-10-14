using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
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
        private BufferLookup<CompletedResearch> _researched;
        private BufferLookup<UpgradeEffect> _effects;
        private BufferLookup<StatModifier> _modifiers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _researched = state.GetBufferLookup<CompletedResearch>(true);
            _effects = state.GetBufferLookup<UpgradeEffect>(true);
            _modifiers = state.GetBufferLookup<StatModifier>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _researched.Update(ref state);
            _effects.Update(ref state);
            _modifiers.Update(ref state);

            new ApplyJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = _researched,
                Effects = _effects,
                Modifiers = _modifiers,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct ApplyJob : IJobEntity
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;
            [ReadOnly] public BufferLookup<UpgradeEffect> Effects;

            // Each entity writes only its own buffer, and only when its set changes, so stat change filters stay quiet.
            [NativeDisableParallelForRestriction] public BufferLookup<StatModifier> Modifiers;

            private void Execute(Entity entity, ref AppliedUpgrades applied, in EntityInfo info, in Faction faction)
            {
                if (applied.Faction != faction.Value)
                {
                    Strip(entity, applied);
                    applied = new AppliedUpgrades { Faction = faction.Value };
                }

                if (!Researched.TryGetBuffer(Players[faction.Value], out var researched))
                {
                    return;
                }

                if (applied.Count >= researched.Length || !Modifiers.HasBuffer(entity))
                {
                    return;
                }

                var modifiers = Modifiers[entity];
                for (var i = applied.Count; i < researched.Length; i++)
                {
                    // Research that only unlocks something has no effects.
                    if (!Effects.TryGetBuffer(researched[i].Prefab, out var effects))
                    {
                        continue;
                    }

                    foreach (var effect in effects)
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
                if (applied.Count > 0 && Modifiers.TryGetBuffer(entity, out var modifiers))
                {
                    StatMath.RemoveKind(modifiers, StatSourceKind.Upgrade);
                }
            }
        }
    }
}
