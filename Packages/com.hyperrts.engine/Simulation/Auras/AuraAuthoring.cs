using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Stats;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>
    /// An aura around this entity: stat bonuses, healing or damage for whoever is inside, plus a presence record games
    /// read for their own effects (jamming, network links, air cover). Auras with the same name never stack.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Aura")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Auras)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class AuraAuthoring : AuthoringBehaviour
    {
        [Tooltip("Aura type; entities in several auras of one name are affected once. Games match it by AuraId.")]
        public string auraName = "Aura";

        [Tooltip("Radius in world units.")]
        [Min(0.1f)]
        public float radius = 15f;

        [Tooltip("Who is affected: relations to the owner and entity kinds.")]
        public AuraTargets affects = AuraTargets.Friendly | AuraTargets.AllKinds;

        [Tooltip("Bonuses while inside; negative percents weaken (e.g. -0.3 Range for jamming).")]
        public List<StatBonus> bonuses = new();

        [Tooltip("Health restored per second while inside.")]
        [Min(0f)]
        public float healPerSecond;

        [Tooltip("Damage dealt per second while inside.")]
        [Min(0f)]
        public float damagePerSecond;

        [Tooltip("Damage type of the damage over time.")]
        public DamageType damageType;

        public int AuraId => EntityInfo.TypeIdFromName(auraName);

        public static void AddAura(EntityWriter writer, in Aura aura,
            IReadOnlyList<StatModifier> bonuses)
        {
            writer.Add(aura);
            var buffer = writer.AddBuffer<GrantedModifier>();
            foreach (var bonus in bonuses)
            {
                buffer.Add(new GrantedModifier { Modifier = bonus });
            }
        }

        public class Baker : Baker<AuraAuthoring>
        {
            public override void Bake(AuraAuthoring authoring)
            {
                var modifiers = StatBonus.ToModifiers(authoring.bonuses, StatSource.Aura(authoring.AuraId));
                var writer = this.Writer();
                AuraAuthoring.AddAura(writer, new Aura
                {
                    AuraId = authoring.AuraId,
                    Radius = authoring.radius,
                    Affects = authoring.affects,
                    HealPerSecond = authoring.healPerSecond,
                    DamagePerSecond = authoring.damagePerSecond,
                    DamageType = authoring.damageType,
                }, modifiers);
            }
        }
    }
}
