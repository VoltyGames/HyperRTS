using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Stats;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>
    /// A one-time research a producer can queue. Once done, its stat bonuses apply to the owner's matching units and
    /// buildings, including ones built later. Put it on its own prefab and list it in a Producer's research options.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Upgrade")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class UpgradeAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public class Effect
        {
            [Tooltip("Unit and building types affected; empty affects everything the owner has.")]
            public List<GameEntityAuthoring> appliesTo = new();

            [Tooltip("Bonuses given to every affected entity.")]
            public List<StatBonus> bonuses = new();
        }

        [Tooltip("Name shown in the HUD; also the upgrade's identity, so keep it unique.")]
        public string displayName;

        [Tooltip("Icon for the research button.")]
        public Texture2D icon;

        [Tooltip("Resources spent when research is queued.")]
        public List<ResourceQuantity> cost = new();

        [Tooltip("Seconds of research.")]
        [Min(0f)]
        public float researchTime = 30f;

        [Tooltip("Buildings the owner must have completed before researching.")]
        public List<GameEntityAuthoring> prerequisites = new();

        [Tooltip("What the upgrade changes.")]
        public List<Effect> effects = new();

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public int TypeId => EntityInfo.TypeIdFromName(DisplayName);

        private void Reset() => displayName = name;

        public class Baker : Baker<UpgradeAuthoring>
        {
            public override void Bake(UpgradeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var text = new FixedString64Bytes();
                text.CopyFromTruncated(authoring.DisplayName);

                var sink = new BakerSink(this, entity);
                UpgradeSetup.Add(ref sink, new EntityInfo { TypeId = authoring.TypeId, Name = text, Icon = authoring.icon },
                    authoring.researchTime, ProducibleBaking.Costs(authoring.cost),
                    ProducibleBaking.Prerequisites(this, authoring.prerequisites), BakeEffects(authoring));
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

    /// <summary>Marks an upgrade prefab: producing it researches it instead of spawning anything.</summary>
    public struct Upgrade : IComponentData { }
}
