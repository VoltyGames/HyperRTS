using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The project's HyperRTS prefabs, gathered once for every <see cref="IPrefabRule"/>.</summary>
    public sealed class PrefabSet
    {
        public PrefabSet(IReadOnlyList<GameObject> prefabs)
        {
            All = prefabs;
            Entities = prefabs.Select(prefab => prefab.GetComponent<GameEntityAuthoring>())
                .Where(entity => entity != null).ToList();
        }

        public IReadOnlyList<GameObject> All { get; }

        /// <summary>The unit and building prefabs among <see cref="All"/>.</summary>
        public IReadOnlyList<GameEntityAuthoring> Entities { get; }

        public static PrefabSet InProject() => new(EditorAssets.HyperRTSPrefabs());
    }
}
