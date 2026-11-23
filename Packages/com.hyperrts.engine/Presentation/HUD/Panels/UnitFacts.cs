using System.Collections.Generic;
using System.Text;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>
    /// What one selected entity says about itself: its weapons, armor and speed from its type's prefab, which host and
    /// client bake alike, and its sight and passengers from the entity, which are replicated. A line appears only
    /// when the component exists.
    /// </summary>
    public sealed class UnitFacts
    {
        private readonly StringBuilder _text = new();

        public List<DetailRow> Weapons { get; } = new();

        public List<DetailRow> Facts { get; } = new();

        public void Read(EntityManager entityManager, Entity entity, Entity prefab)
        {
            Weapons.Clear();
            Facts.Clear();
            if (prefab != Entity.Null)
            {
                ReadWeapons(entityManager, prefab);
                ReadArmor(entityManager, prefab);
                if (entityManager.HasComponent<MovementSpeed>(prefab))
                {
                    AddFact("details.speed", "details.speed-value", entityManager.GetComponentData<MovementSpeed>(prefab).Value);
                }
            }

            if (entityManager.HasComponent<VisionRange>(entity))
            {
                AddFact("details.vision", "details.range-value", entityManager.GetComponentData<VisionRange>(entity).Value);
            }

            ReadPassengers(entityManager, entity, prefab);
        }

        private void ReadWeapons(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasBuffer<WeaponMount>(entity))
            {
                return;
            }

            var mounts = entityManager.GetBuffer<WeaponMount>(entity, true);
            var poses = entityManager.HasBuffer<WeaponPose>(entity)
                ? entityManager.GetBuffer<WeaponPose>(entity, true)
                : default;
            for (var i = 0; i < mounts.Length; i++)
            {
                if (!mounts[i].Enabled)
                {
                    continue;
                }

                var designation = poses.IsCreated && i < poses.Length ? poses[i].Designation.ToString() : "";
                var name = designation.Length == 0 ? HUDText.Text("details.weapon") : HUDText.WeaponName(designation);
                var weapon = mounts[i].Weapon;
                var value = HUDText.Text("details.weapon-line", Targets(weapon.Targets), weapon.Range);
                Weapons.Add(new DetailRow(name, value));
            }
        }

        private string Targets(WeaponTargets targets)
        {
            _text.Clear();
            AppendTarget(targets, WeaponTargets.Ground, "details.target-ground");
            AppendTarget(targets, WeaponTargets.Naval, "details.target-naval");
            AppendTarget(targets, WeaponTargets.Air, "details.target-air");
            AppendTarget(targets, WeaponTargets.Projectile, "details.target-projectile");
            return _text.ToString();
        }

        private void AppendTarget(WeaponTargets targets, WeaponTargets flag, string key)
        {
            if ((targets & flag) == WeaponTargets.None)
            {
                return;
            }

            if (_text.Length > 0)
            {
                _text.Append(", ");
            }

            _text.Append(HUDText.Text(key));
        }

        private void ReadArmor(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasBuffer<ArmorModifier>(entity))
            {
                _text.Clear();
                foreach (var modifier in entityManager.GetBuffer<ArmorModifier>(entity, true))
                {
                    var type = modifier.DamageType.Value;
                    if (type == null || Mathf.Approximately(modifier.Multiplier, 1f))
                    {
                        continue;
                    }

                    _text.Append(_text.Length > 0 ? ", " : "").Append(HUDText.DamageName(type.displayName))
                        .Append(' ').Append(Percent(modifier.Multiplier));
                }

                if (_text.Length > 0)
                {
                    Facts.Add(new DetailRow(HUDText.Text("details.armor"), _text.ToString()));
                }
            }

            if (entityManager.HasComponent<ArmorFacing>(entity))
            {
                var facing = entityManager.GetComponentData<ArmorFacing>(entity);
                var sides = HUDText.Text("details.armor-sides-value", Percent(facing.Front), Percent(facing.Side), Percent(facing.Rear));
                Facts.Add(new DetailRow(HUDText.Text("details.armor-sides"), sides));
            }
        }

        private void ReadPassengers(EntityManager entityManager, Entity entity, Entity prefab)
        {
            if (prefab == Entity.Null || !entityManager.HasComponent<Container>(prefab) || !entityManager.HasBuffer<Cargo>(entity))
            {
                return;
            }

            var container = entityManager.GetComponentData<Container>(prefab);
            var used = Container.Used(entityManager.GetBuffer<Cargo>(entity, true));
            Facts.Add(new DetailRow(HUDText.Text("details.passengers"), HUDText.Text("details.passengers-value", used, container.Capacity)));
        }

        private void AddFact(string nameKey, string valueKey, float value) =>
            Facts.Add(new DetailRow(HUDText.Text(nameKey), HUDText.Text(valueKey, value)));

        private static string Percent(float multiplier) => $"{Mathf.RoundToInt(multiplier * 100f)}%";
    }
}
