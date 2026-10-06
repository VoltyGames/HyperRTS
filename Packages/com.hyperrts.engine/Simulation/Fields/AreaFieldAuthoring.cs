using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Fields
{
    /// <summary>
    /// An aura around this entity: stat bonuses, healing or damage for whoever is inside, plus a presence record games
    /// read for their own effects (jamming, network links, air cover). Fields with the same name never stack.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Area Field")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class AreaFieldAuthoring : MonoBehaviour
    {
        [Tooltip("Field type; entities in several fields of one name are affected once. Games match it by FieldId.")]
        public string fieldName = "Field";

        [Tooltip("Radius in world units.")]
        [Min(0.1f)]
        public float radius = 15f;

        [Tooltip("Who is affected: relations to the owner and entity kinds.")]
        public FieldTargets affects = FieldTargets.Friendly | FieldTargets.AllKinds;

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

        public int FieldId => EntityInfo.TypeIdFromName(fieldName);

        public class Baker : Baker<AreaFieldAuthoring>
        {
            public override void Bake(AreaFieldAuthoring authoring)
            {
                var modifiers = new List<StatModifier>();
                foreach (var bonus in authoring.bonuses)
                {
                    modifiers.Add(bonus.ToModifier(authoring.FieldId));
                }

                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                FieldSetup.AddField(ref sink, new AreaField
                {
                    FieldId = authoring.FieldId,
                    Radius = authoring.radius,
                    Affects = authoring.affects,
                    HealPerSecond = authoring.healPerSecond,
                    DamagePerSecond = authoring.damagePerSecond,
                    DamageType = authoring.damageType,
                }, modifiers);
            }
        }
    }

    /// <summary>An aura applied by <see cref="AreaFieldSystem"/>; its stat bonuses are in <see cref="AreaFieldBonus"/>.</summary>
    public struct AreaField : IComponentData
    {
        public int FieldId;
        public float Radius;
        public FieldTargets Affects;
        public float HealPerSecond;
        public float DamagePerSecond;
        public UnityObjectRef<DamageType> DamageType;
    }

    /// <summary>A modifier the field grants, with the field id as its source.</summary>
    [InternalBufferCapacity(1)]
    public struct AreaFieldBonus : IBufferElementData
    {
        public StatModifier Modifier;
    }
}
