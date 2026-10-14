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
            Parts(parts, name, issues);
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

            Parts(remainsParts, $"{name} remains '{remains.name}'", issues);
        }

        private static void Parts(StretchPartsAuthoring parts, string name, MapIssues issues)
        {
            var problems = 0;
            foreach (var problem in parts.Problems())
            {
                issues.Error($"{name}: stretch parts: {problem}.");
                problems++;
            }

            if (problems == 0)
            {
                Layout(parts, name, issues);
            }
        }

        private static void Layout(StretchPartsAuthoring parts, string name, MapIssues issues)
        {
            try
            {
                parts.Layout();
            }
            catch (InvalidOperationException error)
            {
                issues.Error($"{name}: {error.Message}");
            }
        }
    }
}
