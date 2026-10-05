using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Short text descriptions of an entity, shared by the inspector header and the catalog.</summary>
    public static class EntitySummary
    {
        /// <summary>For example "Unit · Weapon, Builder · 12 DPS · 150 Supplies".</summary>
        public static string Line(GameEntityAuthoring entity)
        {
            var parts = new List<string> { entity is UnitAuthoring ? "Unit" : "Building" };
            var modules = Modules(entity);
            if (modules.Length > 0)
            {
                parts.Add(modules);
            }

            if (entity.TryGetComponent(out WeaponAuthoring weapon))
            {
                parts.Add($"{Dps(weapon):0.#} DPS");
            }

            var cost = CostText(entity);
            parts.Add(cost.Length > 0 ? cost : "free");
            return string.Join(" · ", parts);
        }

        public static float Dps(WeaponAuthoring weapon) => weapon.damage / Mathf.Max(weapon.cooldown, 0.05f);

        public static string CostText(GameEntityAuthoring entity) =>
            string.Join(", ", entity.cost.Where(quantity => quantity.type != null)
                .Select(quantity => $"{quantity.amount} {quantity.type.displayName}"));

        // Other HyperRTS components on the object, named as in the Add Component menu.
        private static string Modules(GameEntityAuthoring entity) =>
            string.Join(", ", entity.GetComponents<MonoBehaviour>()
                .Where(component => component != entity && component != null &&
                                    component.GetType().Namespace?.StartsWith("HyperRTS") == true)
                .Select(component => ObjectNames.NicifyVariableName(component.GetType().Name.Replace("Authoring", ""))));
    }
}
