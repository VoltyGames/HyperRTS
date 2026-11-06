using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Lets a unit or building attack. Without a projectile prefab hits land instantly (melee, hitscan). The stats
    /// are the primary weapon, which sets how the unit engages; extra weapons fire beside it.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class WeaponAuthoring : AuthoringBehaviour
    {
        [Tooltip("What the primary weapon deals and how it reaches; it sets how the unit engages.")]
        public WeaponStats stats = new();

        [Header("Engagement")]
        [Tooltip("A turret aims at the target, so the hull keeps its heading while firing. Buildings always aim a turret.")]
        public bool turreted;

        [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
        [Min(0f)]
        public float acquireRange;

        [Tooltip("Auto-acquired targets are dropped beyond this multiple of the acquire range.")]
        [Min(1f)]
        public float leashFactor = 1.5f;

        [Tooltip("How the unit reacts to enemies when not ordered.")]
        public Stance stance = Stance.Aggressive;

        [Header("Extra weapons")]
        [Tooltip("Weapons mounted beside the primary; each plays its own Fire cue by weapon index.")]
        public List<WeaponSlot> extraWeapons = new();

        /// <summary>A building never turns and an interceptor tracks shots in the air, so their weapons always aim a turret.</summary>
        public bool AimsTurret => turreted || stats.ShootsProjectiles || TryGetComponent<BuildingAuthoring>(out _);

        /// <summary>Weapon mounts the primary and extra weapons take.</summary>
        public int WeaponCount => 1 + extraWeapons.Count;

        /// <summary>An extra weapon: its own stats (with its ammo) plus its targeting and unlock.</summary>
        [Serializable]
        public class WeaponSlot
        {
            [Tooltip("What this weapon deals and how it reaches.")]
            public WeaponStats stats = new();

            [Tooltip("Picks its own target when it can't hit the unit's; otherwise only what the primary hits.")]
            public bool independent;

            [Tooltip("Research the owner must finish before it fires; none fires from the start.")]
            public UpgradeAuthoring requires;
        }

        public class Baker : Baker<WeaponAuthoring>
        {
            public override void Bake(WeaponAuthoring authoring)
            {
                var muzzles = new List<WeaponMuzzle>();
                var weapon = authoring.stats.Bake(this, authoring.transform, muzzles);
                weapon.Aim.Turreted = authoring.AimsTurret;

                var mounts = new List<WeaponMount> { WeaponMount.Create(weapon, authoring.stats.BakeAmmo()) };
                AddExtras(authoring, mounts, muzzles);
                var designations = new List<FixedString32Bytes> { authoring.stats.designation };
                foreach (var slot in authoring.extraWeapons)
                {
                    designations.Add(slot.stats.designation);
                }

                var writer = this.Writer();
                var stance = CombatStance.Create(authoring.stance, authoring.transform.position, authoring.acquireRange, authoring.leashFactor);
                WeaponSetup.Add(writer, mounts, stance, designations);
                if (muzzles.Count > 0)
                {
                    WeaponSetup.AddMuzzles(writer, muzzles);
                }
            }

            private void AddExtras(WeaponAuthoring authoring, List<WeaponMount> mounts, List<WeaponMuzzle> muzzles)
            {
                foreach (var slot in authoring.extraWeapons)
                {
                    if (slot.requires != null)
                    {
                        DependsOn(slot.requires);
                    }

                    var required = slot.requires != null ? slot.requires.TypeId : 0;
                    var weapon = slot.stats.Bake(this, authoring.transform, muzzles);
                    mounts.Add(WeaponMount.CreateExtra(weapon, slot.stats.BakeAmmo(), required, slot.independent));
                }
            }
        }
    }
}
