using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Status
{
    /// <summary>
    /// A timed status effect: stat bonuses, extra damage taken from chosen damage types and damage over time. Put it
    /// on its own prefab and reference that from an ability or game code.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Status Effect")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Status)]
    [DisallowMultipleComponent]
    public class StatusEffectAuthoring : AuthoringBehaviour
    {
        [Tooltip("Name shown in the HUD; also the effect's id, so keep it unique.")]
        public string displayName = "Status Effect";

        [Tooltip("HUD icon.")]
        public Texture2D icon;

        [Tooltip("Seconds the effect lasts after its last application.")]
        [Min(0f)]
        public float duration = 10f;

        [Tooltip("What applying it again while active does.")]
        public StatusStackRule stackRule = StatusStackRule.Refresh;

        [Tooltip("Most stacks the Add Stack rule reaches.")]
        [Min(1)]
        public int maxStacks = 1;

        [Tooltip("Stat bonuses per stack.")]
        public List<StatBonus> bonuses = new();

        [Tooltip("Extra damage taken per stack from the listed damage types.")]
        public List<TypedDamageBonus> damageBonuses = new();

        [Tooltip("Damage per second per stack, credited to whoever applied the effect.")]
        [Min(0f)]
        public float damagePerSecond;

        [Tooltip("Damage type of the damage over time.")]
        public DamageType damageType;

        public int TypeId => EntityInfo.TypeIdFromName(displayName);

        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TWriter>(ref TWriter writer, in EntityInfo info, in StatusEffectDefinition definition,
            IReadOnlyList<StatBonus> bonuses, IReadOnlyList<TypedDamageBonus> damageBonuses)
            where TWriter : struct, IEntityWriter
        {
            var source = StatSource.Status(info.TypeId);
            writer.Add(info);
            writer.Add(definition);

            var stats = writer.AddBuffer<StatusStatBonus>();
            foreach (var bonus in bonuses)
            {
                stats.Add(new StatusStatBonus { Modifier = bonus.ToModifier(source) });
            }

            var damage = writer.AddBuffer<StatusDamageBonus>();
            foreach (var bonus in damageBonuses)
            {
                damage.Add(new StatusDamageBonus { Modifier = bonus.ToModifier(source) });
            }
        }

        public class Baker : Baker<StatusEffectAuthoring>
        {
            public override void Bake(StatusEffectAuthoring authoring)
            {
                var name = new FixedString64Bytes();
                name.CopyFromTruncated(authoring.displayName);
                var info = new EntityInfo { TypeId = authoring.TypeId, Name = name, Icon = authoring.icon };
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.None));
                StatusEffectAuthoring.Add(ref writer, info, new StatusEffectDefinition
                {
                    Duration = authoring.duration,
                    MaxStacks = authoring.maxStacks,
                    Rule = authoring.stackRule,
                    DamagePerSecond = authoring.damagePerSecond,
                    DamageType = authoring.damageType,
                }, authoring.bonuses, authoring.damageBonuses);
            }
        }
    }
}
