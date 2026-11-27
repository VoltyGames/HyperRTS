using System;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.PlayMode.Staging;

/// <summary>
/// The one staging in flight: opens its scene, enters Play, drives a <see cref="StagingRun"/> each frame, leaves
/// Play and reopens the earlier scene. Its progress sits in SessionState, so a domain reload loses nothing.
/// </summary>
[InitializeOnLoad]
internal sealed partial class StagingJob
{
    private const string PendingKey = "HyperRTS.Staging.Pending";
    private const string ActiveKey = "HyperRTS.Staging.Active";
    private const string PreviousSceneKey = "HyperRTS.Staging.PreviousScene";
    private const string StatusKey = "HyperRTS.Staging.Status";
    private const string ResultKey = "HyperRTS.Staging.Result";
    private const string StartedKey = "HyperRTS.Staging.Started";
    private const string ResolutionKey = "HyperRTS.Staging.Resolution";

    // Play starts within this, even behind a script reload, or not at all.
    private const double EntrySeconds = 180;

    [NoAutoStaticsCleanup]
    private static readonly PendingPlay Pending;

    [AutoStaticsCleanup]
    private static StagingJob _current;

    private readonly StagingRun _run;

    static StagingJob()
    {
        Pending = new PendingPlay(PendingKey, Begin);
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private StagingJob(StagingRun run) => _run = run;

    public static bool IsRunning => SessionState.GetBool(ActiveKey, false);

    /// <summary>Where the run is, or how the last one ended.</summary>
    public static string Status => _current != null ? $"Running: {_current._run.Progress}" : SessionState.GetString(StatusKey, "");

    /// <summary>
    /// Opens the staging's scene and enters Play; returns the folder its files will land in. A nonzero
    /// <paramref name="resolution"/> replaces the staging's own, as for a draft.
    /// </summary>
    public static string Start(string path, Vector2Int resolution)
    {
        CheckResolution(resolution);

        EndIfStalled();
        if (IsRunning || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("HyperRTS: stop Play mode, or the running staging, first.");
        }

        var file = StagingFile.Load(path);
        if (!File.Exists(file.Scene))
        {
            throw new FileNotFoundException($"HyperRTS: staging '{file.Name}' names a scene that does not exist: '{file.Scene}'.");
        }

        var active = SceneManager.GetActiveScene();
        if (active.path != file.Scene)
        {
            RequireSavedScenes();
        }

        SessionState.SetString(PreviousSceneKey, active.path);
        SessionState.SetString(StatusKey, $"Starting '{file.Name}'");
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
        SessionState.SetIntArray(ResolutionKey, [resolution.x, resolution.y]);
        if (active.path != file.Scene)
        {
            EditorSceneManager.OpenScene(file.Scene, OpenSceneMode.Single);
        }

        // A frame after opening the scene, so its objects wake in Play instead of half-loaded.
        EditorApplication.delayCall += () =>
        {
            Pending.Enter(path);
            EditorApplication.update += WatchEntry;
        };
        return file.Folder;
    }

    /// <summary>Ends a staging that was started but never reached Play, so the next one can start.</summary>
    public static void EndIfStalled()
    {
        var waited = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f);
        if (IsRunning && _current == null && !EditorApplication.isPlayingOrWillChangePlaymode && waited > EntrySeconds)
        {
            Restore("Failed: the staging never entered Play");
        }
    }

    private static void CheckResolution(Vector2Int resolution)
    {
        var negative = resolution.x < 0 || resolution.y < 0;
        var half = (resolution.x == 0) != (resolution.y == 0);
        if (negative || half)
        {
            throw new ArgumentException($"HyperRTS: a staging resolution needs both width and height in pixels, not {resolution.x}x{resolution.y}.");
        }
    }

    private static void RequireSavedScenes()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("HyperRTS: open scenes have unsaved changes; save or discard them before a staging swaps the scene.");
            }
        }
    }

    private static void Begin(string path)
    {
        try
        {
            var size = SessionState.GetIntArray(ResolutionKey, [0, 0]);
            _current = new StagingJob(new StagingRun(StagingFile.Load(path), new Vector2Int(size[0], size[1])));
        }
        catch (Exception exception)
        {
            SessionState.SetString(ResultKey, $"Failed: {exception.Message}");
            EditorApplication.ExitPlaymode();
            return;
        }

        EditorApplication.update += _current.Tick;
    }

    // Play that never started (compile errors) leaves nothing else to report it.
    private static void WatchEntry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.update -= WatchEntry;
            }

            return;
        }

        EditorApplication.update -= WatchEntry;
        Restore("Failed: Play mode did not start; see the Console.");
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode && _current != null)
        {
            _current.Finish($"Play ended early ({_current._run.Progress})");
        }

        if (change == PlayModeStateChange.EnteredEditMode && IsRunning)
        {
            Restore(SessionState.GetString(ResultKey, "Failed: Play ended early, before the staging began"));
        }
    }

    private static void Restore(string result)
    {
        var scene = SessionState.GetString(PreviousSceneKey, "");
        if (scene.Length > 0 && SceneManager.GetActiveScene().path != scene)
        {
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        }

        SessionState.EraseString(ResultKey);
        SessionState.SetString(StatusKey, result);
        SessionState.SetBool(ActiveKey, false);
        Debug.Log($"HyperRTS: staging: {result}");
    }

    private void Tick()
    {
        try
        {
            if (_run.Tick())
            {
                Finish(null);
            }
        }
        catch (Exception exception)
        {
            Finish(exception.Message);
        }
    }

    private void Finish(string failure)
    {
        EditorApplication.update -= Tick;
        _current = null;
        var path = _run.End(failure);
        SessionState.SetString(ResultKey, failure == null ? $"Done: {path}" : $"Failed: {failure}. Report: {path}");
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
