using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>The project's authoring prefabs, gathered once for every <see cref="Rules.IPrefabRule"/>.</summary>
    public sealed class PrefabSet
    {
        // Finding the authoring prefabs scans every prefab's dependencies, most of a validation pass.
        [AutoStaticsCleanup]
        private static PrefabSet _project;

        public PrefabSet(IReadOnlyList<GameObject> prefabs)
        {
            All = prefabs;
            Entities = EditorAssets.EntityPrefabs(prefabs);
        }

        public IReadOnlyList<GameObject> All { get; }

        /// <summary>The unit and building prefabs among <see cref="All"/>.</summary>
        public IReadOnlyList<GameEntityAuthoring> Entities { get; }

        /// <summary>The project's set, kept until the next import (<see cref="ValidationImport"/>).</summary>
        public static PrefabSet InProject() => _project ??= new PrefabSet(EditorAssets.AuthoringPrefabs());

        internal static void Forget() => _project = null;
    }
}
