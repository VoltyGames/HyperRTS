using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Generators;
using HyperRTS.Editor.Stages;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Rows
{
    /// <summary>Draws a dot on the Project window entry of a generator item that is out of date or failed, found once and kept until the project changes or a run is recorded.</summary>
    internal sealed class ProjectGeneratorDots
    {
        private const float ListRowHeight = 20f;
        private const float DotSize = 6f;
        private const float DotMargin = 3f;

        // The Hierarchy rows' warning and error dot colours.
        [NoAutoStaticsCleanup]
        private static readonly Color OutOfDateColor = new Color32(196, 136, 0, 255);

        [NoAutoStaticsCleanup]
        private static readonly Color FailedColor = new Color32(222, 64, 52, 255);

        private Dictionary<string, Color> _dots;
        private bool _repaintQueued;

        [InitializeOnLoadMethod]
        private static void Install()
        {
            var dots = new ProjectGeneratorDots();
            EditorApplication.projectWindowItemOnGUI += dots.Draw;
            EditorApplication.projectChanged += dots.Clear;
            GeneratorRunLog.Recorded += dots.ClearAndRepaint;
        }

        private void Clear() => _dots = null;

        // A run records many items; one repaint after them rebuilds the dots once.
        private void ClearAndRepaint()
        {
            _dots = null;
            if (_repaintQueued)
            {
                return;
            }

            _repaintQueued = true;
            EditorApplication.delayCall += () =>
            {
                _repaintQueued = false;
                EditorApplication.RepaintProjectWindow();
            };
        }

        private void Draw(string guid, Rect row)
        {
            if (!EditorState.instance.projectGeneratorStates)
            {
                return;
            }

            if (_dots == null)
            {
                // The run's last record queues the repaint that builds them.
                if (GeneratorRunner.Running)
                {
                    return;
                }

                _dots = BuildDots();
            }

            if (!_dots.TryGetValue(guid, out var color))
            {
                return;
            }

            // The list view centres the dot on its line; the grid view puts it in the thumbnail's corner.
            var y = row.height > ListRowHeight ? row.y + DotMargin : row.y + (row.height - DotSize) / 2f;
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
                        dots[guid] = failed ? FailedColor : OutOfDateColor;
                    }
                }
            }

            return dots;
        }
    }
}
