using System.Collections.Generic;
using HyperRTS.Editor.Generators;
using HyperRTS.Editor.Stages;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Decorations;

/// <summary>Draws a dot on the Project window entry of a generator item that is out of date or failed, found after a project change or a recorded run.</summary>
internal sealed class ProjectGeneratorDots
{
    private const float DotSize = 6f;
    private const float DotMargin = 3f;

    private Dictionary<string, Color> _dots = [];
    private bool _rebuildQueued;

    [InitializeOnLoadMethod]
    private static void Install()
    {
        var dots = new ProjectGeneratorDots();
        EditorApplication.projectWindowItemOnGUI += dots.Draw;
        EditorApplication.projectChanged += dots.QueueRebuild;
        GeneratorRunLog.Recorded += dots.QueueRebuild;
        dots.QueueRebuild();
    }

    // Building reads every generator's input folders, so it runs outside OnGUI. A run records many items and an
    // import raises many changes: they share one rebuild, which waits for a running generator to finish.
    private void QueueRebuild()
    {
        if (_rebuildQueued)
        {
            return;
        }

        _rebuildQueued = true;
        EditorApplication.delayCall += Rebuild;
    }

    private void Rebuild()
    {
        if (GeneratorRunner.Running)
        {
            EditorApplication.delayCall += Rebuild;
            return;
        }

        _rebuildQueued = false;
        _dots = BuildDots();
        EditorApplication.RepaintProjectWindow();
    }

    private void Draw(string guid, Rect row)
    {
        if (!DecorationSettings.instance.projectGeneratorStates || !_dots.TryGetValue(guid, out var color))
        {
            return;
        }

        // The list view centres the dot on its line; the grid view puts it in the thumbnail's corner.
        var y = row.height > DecorationStyle.ListRowHeight ? row.y + DotMargin : row.y + (row.height - DotSize) / 2f;
        var dot = new Rect(row.xMax - DotSize - DotMargin, y, DotSize, DotSize);
        GUI.DrawTexture(dot, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, DotSize / 2f);
    }

    // Every item's state once, keyed by the GUID of its project path; current items get no entry.
    private static Dictionary<string, Color> BuildDots()
    {
        var dots = new Dictionary<string, Color>();
        var log = GeneratorRunLog.Load();
        foreach (var generator in GeneratorRunner.All())
        {
            foreach (var item in generator.Items)
            {
                var state = generator.State(item, log);
                if (state == StageState.Done)
                {
                    continue;
                }

                // Items sharing an entry show the worst state.
                var guid = AssetDatabase.AssetPathToGUID(item.ProjectPath);
                var failed = state == StageState.Failed;
                if (failed || !dots.ContainsKey(guid))
                {
                    dots[guid] = DecorationStyle.DotColor(failed ? MessageType.Error : MessageType.Warning);
                }
            }
        }

        return dots;
    }
}
