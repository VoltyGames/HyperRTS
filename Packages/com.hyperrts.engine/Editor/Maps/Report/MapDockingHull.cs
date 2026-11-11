using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Resources.Gathering;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>The largest hull that docks: the longest and widest of the project's harvesting units.</summary>
    public readonly struct MapDockingHull
    {
        public readonly float Length;
        public readonly float Radius;

        /// <summary>The prefabs the length and width come from, for issues.</summary>
        public readonly string Prefabs;

        public MapDockingHull(float length, float radius, string prefabs)
        {
            Length = length;
            Radius = radius;
            Prefabs = prefabs;
        }

        /// <summary>Half the core segment, from the hull's centre to where its end cap starts.</summary>
        public float HalfSegment => NavAgent.HalfSegmentFor(Radius, Length);

        /// <summary>The hull of every unit prefab under Assets that harvests; false when none does.</summary>
        public static bool TryFind(out MapDockingHull hull)
        {
            hull = default;
            var units = EditorAssets.PrefabsUsing(type => type == typeof(HarvesterAuthoring), "Assets")
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab.TryGetComponent<HarvesterAuthoring>(out _))
                .Select(prefab => prefab.GetComponent<UnitAuthoring>())
                .Where(unit => unit != null)
                .ToList();
            if (units.Count == 0)
            {
                return false;
            }

            // A hull no longer than its width is round, as the agent bakes it.
            var longest = units.OrderByDescending(unit => Mathf.Max(unit.hullLength, unit.radius * 2f)).First();
            var widest = units.OrderByDescending(unit => unit.radius).First();
            var length = Mathf.Max(longest.hullLength, longest.radius * 2f, widest.radius * 2f);
            var prefabs = string.Join(" and ", new[] { longest.name, widest.name }.Distinct());
            hull = new MapDockingHull(length, widest.radius, prefabs);
            return true;
        }
    }
}
