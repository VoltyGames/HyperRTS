using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// Cooldown abilities used from the command card (grenades, smoke, airstrikes, superweapons). On a building with
    /// unlimited range an ability works as a support power.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Abilities")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class AbilityAuthoring : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Name shown on the button; also the ability's id, so keep it unique per entity.")]
            public string name = "Ability";

            [Tooltip("Button icon.")]
            public Texture2D icon;

            [Tooltip("What the ability is aimed at.")]
            public AbilityTarget target = AbilityTarget.Point;

            [Tooltip("Which entities an entity-targeted ability accepts.")]
            public AbilityTargetFilter filter = AbilityTargetFilter.Hostile;

            [Tooltip("Cast range, edge to edge; units walk into range first. 0 = unlimited.")]
            [Min(0f)]
            public float range = 10f;

            [Tooltip("Seconds before it can be used again.")]
            [Min(0f)]
            public float cooldown = 30f;

            [Tooltip("Start on cooldown (superweapons charge up first).")]
            public bool startsOnCooldown;

            [Tooltip("Building the owner must have completed to use it.")]
            public GameEntityAuthoring requires;

            [Header("Built-in effects")]
            [Tooltip("Prefab spawned at the target (mine, beacon, incoming aircraft), owned by the caster's player.")]
            public GameObject spawnPrefab;

            [Tooltip("Damage dealt around the target; negative heals.")]
            public float damage;

            [Tooltip("Radius of the damage; 0 hits only an entity target.")]
            [Min(0f)]
            public float radius;

            [Tooltip("Damage type that armor scales.")]
            public DamageType damageType;

            [Tooltip("The damage also hurts the caster's allies.")]
            public bool friendlyFire = true;
        }

        [Tooltip("Abilities in command-card order.")]
        public List<Entry> abilities = new();

        public class Baker : Baker<AbilityAuthoring>
        {
            public override void Bake(AbilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Ability>(entity);
                foreach (var entry in authoring.abilities)
                {
                    buffer.Add(BakeEntry(entry));
                }
            }

            private Ability BakeEntry(Entry entry)
            {
                var name = new FixedString32Bytes();
                name.CopyFromTruncated(entry.name);
                if (entry.requires != null)
                {
                    DependsOn(entry.requires);
                }

                return new Ability
                {
                    Id = EntityInfo.TypeIdFromName(entry.name),
                    Name = name,
                    Icon = entry.icon,
                    Target = entry.target,
                    Filter = entry.filter,
                    Range = entry.range,
                    Cooldown = entry.cooldown,
                    CooldownRemaining = entry.startsOnCooldown ? entry.cooldown : 0f,
                    RequiredTypeId = entry.requires != null ? entry.requires.TypeId : 0,
                    SpawnPrefab = entry.spawnPrefab != null
                        ? GetEntity(entry.spawnPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    Damage = entry.damage,
                    Radius = entry.radius,
                    DamageType = entry.damageType,
                    FriendlyFire = entry.friendlyFire,
                };
            }
        }
    }
}
