using System;
using HyperRTS.Editor.Maps.Build;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>
    /// Batch-mode map build: <c>-executeMethod HyperRTS.Editor.Maps.Tools.MapCommandLine.Build -map &lt;spec path&gt;</c>.
    /// Exits 0 when the report passes, 1 on report errors, 2 when the build throws.
    /// </summary>
    public static class MapCommandLine
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-map");
            if (index < 0 || index + 1 >= args.Length)
            {
                Debug.LogError("HyperRTS: pass the spec to build with -map <path>.");
                EditorApplication.Exit(2);
                return;
            }

            try
            {
                var report = MapBuilder.Build(args[index + 1]);
                EditorApplication.Exit(report.Passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }
    }
}
