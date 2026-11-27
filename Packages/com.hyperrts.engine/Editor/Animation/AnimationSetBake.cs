using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Animation;

/// <summary>What the bake commands share: baking one named set or every set.</summary>
public static class AnimationSetBake
{
    /// <summary>The largest width or height the GPU texture limit allows a baked animation texture.</summary>
    internal const int MaxTextureSize = 16384;

    /// <summary>Bakes the set named or at a path, or every set when the name is empty; returns each set with what it wrote.</summary>
    public static object Bake<T>(string set) where T : ScriptableObject, IAnimationSet
    {
        var sets = string.IsNullOrEmpty(set) ? EditorAssets.FindAssets<T>() : new List<T> { Find<T>(set) };
        return sets.Select(found => new { set = AssetDatabase.GetAssetPath(found), written = found.Bake() }).ToList();
    }

    private static T Find<T>(string set) where T : ScriptableObject
    {
        return EditorAssets.FindAssets<T>().Find(candidate => candidate.name == set || AssetDatabase.GetAssetPath(candidate) == set)
            ?? throw new FileNotFoundException($"No {typeof(T).Name} '{set}'.");
    }
}
