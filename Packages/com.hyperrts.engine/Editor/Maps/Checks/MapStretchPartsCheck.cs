using System;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Simulation.Combat;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// A palette prefab with <see cref="StretchPartsAuthoring"/> names all three parts as children of its root, and its
    /// remains carry the parts too so they stretch to match.
    /// </summary>
    public static class MapStretchPartsCheck
    {
        public static void Check(MapPaletteEntry entry, MapIssues issues)
        {
            if (entry.prefab == null || !entry.prefab.TryGetComponent<StretchPartsAuthoring>(out var parts))
            {
                return;
            }

            var name = $"Palette entry '{entry.key}'";
            if (Parts(parts, name, issues, out var layout))
            {
                Axis(entry, parts, layout, name, issues);
            }

            if (!entry.prefab.TryGetComponent<DestructibleAuthoring>(out var destructible) || destructible.remains == null)
            {
                return;
            }

            var remains = destructible.remains;
            if (!remains.TryGetComponent<StretchPartsAuthoring>(out var remainsParts))
            {
                issues.Error($"{name}: remains '{remains.name}' need Stretch Parts too, or they won't match a stretched '{entry.prefab.name}'.");
                return;
            }

            Parts(remainsParts, $"{name} remains '{remains.name}'", issues, out _);
        }

        private static bool Parts(StretchPartsAuthoring parts, string name, MapIssues issues, out StretchLayout layout)
        {
            layout = default;
            var problems = 0;
            foreach (var problem in parts.Problems())
            {
                issues.Error($"{name}: stretch parts: {problem}.");
                problems++;
            }

            return problems == 0 && Layout(parts, name, issues, out layout);
        }

        private static bool Layout(StretchPartsAuthoring parts, string name, MapIssues issues, out StretchLayout layout)
        {
            try
            {
                layout = parts.Layout();
                return true;
            }
            catch (InvalidOperationException error)
            {
                issues.Error($"{name}: {error.Message}");
                layout = default;
                return false;
            }
        }

        /// <summary>The planners stretch along the size's long side, so the parts must lie along it too.</summary>
        private static void Axis(MapPaletteEntry entry, StretchPartsAuthoring parts, StretchLayout layout, string name,
            MapIssues issues)
        {
            var axis = parts.transform.InverseTransformDirection(parts.startCap.parent.TransformDirection(layout.Axis));
            var partsAlongZ = Mathf.Abs(axis.z) > Mathf.Abs(axis.x);
            var sizeAlongZ = entry.size.y >= entry.size.x;
            if (partsAlongZ == sizeAlongZ)
            {
                return;
            }

            issues.Error($"{name}: stretch parts lie along {AxisName(partsAlongZ)}, but its size {entry.size} puts " +
                $"the long side along {AxisName(sizeAlongZ)}.");
        }

        private static string AxisName(bool alongZ) => alongZ ? "Z" : "X";
    }
}
