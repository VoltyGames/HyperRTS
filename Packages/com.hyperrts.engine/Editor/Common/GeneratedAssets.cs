using System;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common
{
    /// <summary>The <c>Generated</c> label on assets a tool writes from a source, so they show as never to be hand-edited.</summary>
    public static class GeneratedAssets
    {
        public const string Label = "Generated";

        public static void Mark(string path) => Mark(Load(path));

        public static void Mark(Object asset)
        {
            var labels = AssetDatabase.GetLabels(asset);
            if (!labels.Contains(Label))
            {
                AssetDatabase.SetLabels(asset, labels.Append(Label).ToArray());
            }
        }

        public static bool IsMarked(string path) => AssetDatabase.GetLabels(Load(path)).Contains(Label);

        private static Object Load(string path) =>
            AssetDatabase.LoadMainAssetAtPath(path) ?? throw new InvalidOperationException($"HyperRTS: no asset at {path}.");
    }
}
