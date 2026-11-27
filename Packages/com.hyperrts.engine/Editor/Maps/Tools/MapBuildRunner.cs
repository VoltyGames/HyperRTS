using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Tools;

/// <summary>
/// Builds maps for the Tools window and the commands: saves or keeps the open scenes first and puts the scenes
/// back afterwards.
/// </summary>
public static class MapBuildRunner
{
    /// <summary>
    /// Builds one spec. With <paramref name="ask"/> the user may save or cancel; without it, unsaved scenes
    /// throw, since a build opens its own scene and an agent can't answer a dialog.
    /// </summary>
    public static MapReport Build(string spec, bool ask = true)
    {
        if (!ScenesSaved(ask))
        {
            return null;
        }

        var scene = EditorSceneManager.GetActiveScene().path;
        var result = MapBuilder.Build(spec);
        Reopen(scene);
        return result;
    }

    /// <summary>Builds every spec in the project with a progress bar.</summary>
    public static List<MapReport> BuildAll(bool ask = true) => BuildEach(MapPaths.All(), ask);

    /// <summary>
    /// Builds every listed map whose scene is missing, as in a fresh clone, since build output stays out of
    /// version control.
    /// </summary>
    public static List<MapReport> BuildMissing(bool ask = true) => BuildEach(MapPaths.All().Where(IsMissing).ToArray(), ask);

    private static bool IsMissing(string spec)
    {
        var map = MapSpecJson.Read(File.ReadAllText(spec));
        return map.listed && !File.Exists(new MapPaths(spec, map.name).Scene);
    }

    private static List<MapReport> BuildEach(string[] specs, bool ask)
    {
        var results = new List<MapReport>();
        if (specs.Length == 0 || !ScenesSaved(ask))
        {
            return results;
        }

        var scene = EditorSceneManager.GetActiveScene().path;
        try
        {
            for (var i = 0; i < specs.Length; i++)
            {
                EditorUtility.DisplayProgressBar("HyperRTS", "Building " + MapPaths.MapName(specs[i]), (float)i / specs.Length);
                results.Add(MapBuilder.Build(specs[i]));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Reopen(scene);
        }

        return results;
    }

    private static bool ScenesSaved(bool ask)
    {
        if (ask)
        {
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        var unsaved = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(scene => scene.isDirty).ToList();
        if (unsaved.Count > 0)
        {
            var names = string.Join(", ", unsaved.Select(scene => string.IsNullOrEmpty(scene.path) ? scene.name : scene.path));
            throw new InvalidOperationException($"HyperRTS: save or discard the changes to {names} first: a map build opens its own scene.");
        }

        return true;
    }

    private static void Reopen(string scene)
    {
        if (!string.IsNullOrEmpty(scene) && EditorSceneManager.GetActiveScene().path != scene)
        {
            EditorSceneManager.OpenScene(scene);
        }
    }
}
