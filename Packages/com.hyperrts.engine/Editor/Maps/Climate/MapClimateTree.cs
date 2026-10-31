using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A climate tree in one or more model variants, each with an optional entity version for the playable area.</summary>
    [MapLibraryItem("tree")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Tree", fileName = "New.tree")]
    public sealed class MapClimateTree : MapLibraryItem
    {
        [Tooltip("Mesh prefab per variant, ideally with an LOD Group, origin at the base of the trunk.")]
        public List<GameObject> prefabs = new();

        [Tooltip("Destructible entity version of each variant, in the same order, used inside the playable area. Empty keeps terrain trees everywhere.")]
        public List<GameObject> destructibles = new();

        [Tooltip("Random uniform scale range.")]
        public Vector2 scale = new(0.85f, 1.15f);

        public bool HasEntities => destructibles.Count > 0;

        /// <summary>The variant a planted tree's seed picks.</summary>
        public int Variant(uint seed)
        {
            if (prefabs.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: tree '{key}' has no prefabs to pick a variant from.");
            }

            return (int)(seed % (uint)prefabs.Count);
        }

        protected internal override void Load()
        {
            RequireFilled(prefabs, "prefabs");
            RequireFilled(destructibles, "destructibles");
            var matched = destructibles.Count == 0 || destructibles.Count == prefabs.Count;
            if (!matched)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library asset '{file}' has {destructibles.Count} destructibles for " +
                    $"{prefabs.Count} prefabs; give one per variant or none.");
            }
        }
    }
}
