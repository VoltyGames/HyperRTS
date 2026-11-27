using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Sets;

/// <summary>The project's authoring prefabs, gathered once for every <see cref="Rules.IPrefabRule"/>.</summary>
public sealed partial class PrefabSet
{
    // Finding the authoring prefabs scans every prefab's dependencies, most of a validation pass.
    [AutoStaticsCleanup]
    private static PrefabSet _project;

    private HashSet<GameObject> _bases;

    public PrefabSet(IReadOnlyList<GameObject> prefabs)
    {
        All = prefabs;
        Entities = EditorAssets.EntityPrefabs(prefabs);
    }

    public IReadOnlyList<GameObject> All { get; }

    /// <summary>The unit and building prefabs among <see cref="All"/>.</summary>
    public IReadOnlyList<GameEntityAuthoring> Entities { get; }

    /// <summary>Whether a prefab in the set is a variant of <paramref name="prefab"/>, which then exists to be derived from.</summary>
    public bool IsBase(GameObject prefab)
    {
        _bases ??= All.Select(PrefabUtility.GetCorrespondingObjectFromSource).Where(source => source != null).ToHashSet();
        return _bases.Contains(prefab);
    }

    /// <summary>The project's set, kept until the next import (<see cref="ValidationImport"/>).</summary>
    public static PrefabSet InProject() => _project ??= new PrefabSet(EditorAssets.AuthoringPrefabs());

    internal static void Forget() => _project = null;
}
