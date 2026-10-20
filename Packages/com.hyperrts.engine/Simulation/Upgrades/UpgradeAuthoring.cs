using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>
    /// A one-time research a producer can queue. Once done, its stat bonuses apply to the owner's matching units and
    /// buildings, including ones built later. Put it on its own prefab and list it in a Producer's research options.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Research + "Upgrade")]
    [Icon(HyperRTSIcons.Research)]
    [HelpURL(HyperRTSDocs.Upgrades)]
    [DisallowMultipleComponent]
    public class UpgradeAuthoring : ProducibleAuthoring
    {
        [Serializable]
        public class Effect
        {
            [Tooltip("Unit and building types affected; empty affects everything the owner has.")]
            public List<GameEntityAuthoring> appliesTo = new();

            [Tooltip("Bonuses given to every affected entity.")]
            public List<StatBonus> bonuses = new();
        }

        [Header("Research")]
        [Tooltip("What the upgrade changes.")]
        public List<Effect> effects = new();

        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TWriter>(ref TWriter writer, in EntityInfo info, float buildTime,
            IReadOnlyList<ResourceCost> cost, IReadOnlyList<Prerequisite> prerequisites,
            IReadOnlyList<UpgradeEffect> effects) where TWriter : struct, IEntityWriter
        {
            writer.Add<Research>();
            writer.Add(info);
            writer.Add(new Producible { BuildTime = buildTime });

            writer.AddBuffer(cost);
            writer.AddBuffer(prerequisites);
            writer.AddBuffer(effects);
        }

        public class Baker : Baker<UpgradeAuthoring>
        {
            public override void Bake(UpgradeAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.None));
                var info = authoring.BakeProducible(this, out var costs, out var required);
                UpgradeAuthoring.Add(ref writer, info, authoring.buildTime, costs, required, BakeEffects(authoring));
            }

            private List<UpgradeEffect> BakeEffects(UpgradeAuthoring authoring)
            {
                var effects = new List<UpgradeEffect>();
                foreach (var effect in authoring.effects)
                {
                    foreach (var bonus in effect.bonuses)
                    {
                        var modifier = bonus.ToModifier(StatSource.Upgrade(authoring.TypeId));
                        if (effect.appliesTo.Count == 0)
                        {
                            effects.Add(new UpgradeEffect { Modifier = modifier });
                        }

                        foreach (var target in effect.appliesTo)
                        {
                            if (target != null)
                            {
                                DependsOn(target);
                                effects.Add(new UpgradeEffect { AppliesTo = target.TypeId, Modifier = modifier });
                            }
                        }
                    }
                }

                return effects;
            }
        }
    }
}
