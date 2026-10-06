using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.GameEntities;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The project's authoring prefabs, gathered once for every <see cref="IPrefabRule"/>.</summary>
    public sealed class PrefabSet
    {
        public PrefabSet(IReadOnlyList<GameObject> prefabs)
        {
            All = prefabs;
            Entities = EditorAssets.EntityPrefabs(prefabs);
        }

        public IReadOnlyList<GameObject> All { get; }

        /// <summary>The unit and building prefabs among <see cref="All"/>.</summary>
        public IReadOnlyList<GameEntityAuthoring> Entities { get; }

        public static PrefabSet InProject() => new(EditorAssets.AuthoringPrefabs());
    }
}
