using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>An entity's role, stats and cost as text, shared by the inspector card and the catalog.</summary>
    public static class EntitySummary
    {
        /// <summary>For example "Unit · Weapon, Builder": the kind, then its other authoring components.</summary>
        public static string Role(GameEntityAuthoring entity)
        {
            var modules = Modules(entity);
            var kind = ComponentName(entity.GetType());
            return modules.Length > 0 ? $"{kind} · {modules}" : kind;
        }

        /// <summary>The label and value of each stat the entity has, such as Health, Speed or DPS.</summary>
        public static List<(string Label, string Value)> Stats(GameEntityAuthoring entity)
        {
            var stats = new List<(string, string)>();
            var cost = CostText(entity);
            if (cost.Length > 0)
            {
                stats.Add(("Cost", cost));
            }

            if (entity.buildTime > 0f)
            {
                stats.Add(("Build time", $"{entity.buildTime:0.#} s"));
            }

            stats.Add(("Health", $"{entity.maxHealth:0.#}"));
            if (entity.TryGetComponent(out ArmorAuthoring armor) && ArmorText(armor).Length > 0)
            {
                stats.Add(("Armor", ArmorText(armor)));
            }

            if (entity is UnitAuthoring unit)
            {
                stats.Add(("Speed", $"{unit.moveSpeed:0.#} m/s"));
            }

            stats.Add(("Vision", $"{entity.visionRange:0.#} m"));
            if (entity.TryGetComponent(out WeaponAuthoring weapon))
            {
                stats.Add(("DPS", $"{Dps(weapon):0.#}"));
                stats.Add(("Range", $"{Range(weapon):0.#} m"));
            }

            return stats;
        }

        /// <summary>Damage taken as front, side and rear percentages; else how many damage types it changes.</summary>
        public static string ArmorText(ArmorAuthoring armor)
        {
            if (armor.IsDirectional)
            {
                return $"{armor.front * 100f:0} / {armor.side * 100f:0} / {armor.rear * 100f:0} %";
            }

            return armor.modifiers.Count > 0 ? $"{armor.modifiers.Count} damage types" : "";
        }

        /// <summary>The longest reach of the primary and every extra weapon.</summary>
        public static float Range(WeaponAuthoring weapon) =>
            weapon.extraWeapons.Aggregate(weapon.stats.range, (longest, slot) => Mathf.Max(longest, slot.stats.range));

        /// <summary>The primary's and every extra weapon's damage per second, added up.</summary>
        public static float Dps(WeaponAuthoring weapon) =>
            weapon.stats.Dps + weapon.extraWeapons.Sum(slot => slot.stats.Dps);

        public static string CostText(GameEntityAuthoring entity) => CostText(entity.cost);

        public static string CostText(IEnumerable<ResourceQuantity> cost) =>
            string.Join(", ", cost.Where(quantity => quantity.type != null)
                .Select(quantity => $"{quantity.amount} {quantity.type.displayName}"));

        /// <summary>For example "150 Supplies · 12s", for a unit, building or upgrade; empty when unassigned.</summary>
        public static string ProducibleText(ProducibleAuthoring producible)
        {
            if (producible == null)
            {
                return "";
            }

            var costText = CostText(producible.cost);
            return $"{(costText.Length > 0 ? costText : "free")} · {producible.buildTime:0.#}s";
        }

        public static Texture2D ProducibleIcon(ProducibleAuthoring producible) =>
            producible != null ? producible.icon : null;

        // Other authoring components on the object (engine or game), named as in the Add Component menu.
        // Every selectable has a pick box, so it says nothing about the entity.
        private static string Modules(GameEntityAuthoring entity) =>
            string.Join(", ", entity.GetComponents<AuthoringBehaviour>()
                .Where(component => component != entity && component is not PickBoxAuthoring)
                .Select(component => ComponentName(component.GetType())));

        /// <summary>A component type's name without "Authoring", as the inspector chips and Add Component list show it.</summary>
        public static string ComponentName(System.Type type) =>
            ObjectNames.NicifyVariableName(type.Name.Replace("Authoring", ""));
    }
}
