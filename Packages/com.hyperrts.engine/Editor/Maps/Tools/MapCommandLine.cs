using System;
using HyperRTS.Editor.Maps.Build;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>
    /// Batch-mode map tools: <c>-executeMethod HyperRTS.Editor.Maps.Tools.MapCommandLine.Build -map &lt;spec path&gt;</c>
    /// (or <c>.Preview</c>). Exits 0 when the report passes, 1 on report errors, 2 when the tool throws.
    /// </summary>
    public static class MapCommandLine
    {
        public static void Build() => Run(spec => MapBuilder.Build(spec));

        public static void Preview() => Run(MapPreview.Run);

        private static void Run(Func<string, Report.MapReport> tool)
        {
            var args = System.Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-map");
            if (index < 0 || index + 1 >= args.Length)
            {
                Debug.LogError("HyperRTS: pass the spec with -map <path>.");
                EditorApplication.Exit(2);
                return;
            }

            try
            {
                var report = tool(args[index + 1]);
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
