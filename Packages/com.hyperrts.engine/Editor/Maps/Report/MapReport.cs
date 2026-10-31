using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// A build's (or preview's) result as <c>Logs/MapReports/&lt;Map&gt;.json</c> (<c>.preview.json</c>) stores it,
    /// next to the overview image.
    /// </summary>
    [Serializable]
    public sealed class MapReport
    {
        public const string PreviewSuffix = ".preview";

        public string map;
        public string status;
        public string built;
        public List<string> errors = new();
        public List<string> warnings = new();

        /// <summary>The spec feature of each error and warning, index for index (empty array name: the whole map).</summary>
        public List<MapIssueFeature> errorFeatures = new();
        public List<MapIssueFeature> warningFeatures = new();

        public MapMetrics metrics;
        public MapLandFigures land;

        public bool Passed => errors.Count == 0;

        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MapReports"));

        public static string JsonPath(string map) => Path.Combine(Folder, map + ".json");

        public static string ImagePath(string map) => Path.Combine(Folder, map + ".png");

        /// <summary>Where a map's standard screenshots go.</summary>
        public static string ViewsFolder(string map) => Path.Combine(Folder, map);

        public static MapReport Create(MapSpec spec, MapIssues issues, MapMetrics metrics) => new()
        {
            map = spec.name,
            status = spec.status,
            built = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            errors = issues.Errors.Select(e => e.Message).ToList(),
            warnings = issues.Warnings.Select(w => w.Message).ToList(),
            errorFeatures = issues.Errors.Select(e => e.Feature).ToList(),
            warningFeatures = issues.Warnings.Select(w => w.Feature).ToList(),
            metrics = metrics,
        };

        /// <summary>The last report written for a map, or null if it was never built.</summary>
        public static MapReport Load(string map) => LoadFile(JsonPath(map));

        /// <summary>The last preview written for a map, or null if it was never previewed.</summary>
        public static MapReport LoadPreview(string map) => LoadFile(JsonPath(map + PreviewSuffix));

        private static MapReport LoadFile(string path)
        {
            return File.Exists(path) ? JsonUtility.FromJson<MapReport>(File.ReadAllText(path)) : null;
        }

        public void Write(Texture2D overview) => Write(map, overview);

        /// <summary>A preview's report and image, kept apart from the last build's.</summary>
        public void WritePreview(Texture2D overview) => Write(map + PreviewSuffix, overview);

        private void Write(string name, Texture2D overview)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(JsonPath(name), JsonUtility.ToJson(this, true));
            if (overview != null)
            {
                File.WriteAllBytes(ImagePath(name), overview.EncodeToPNG());
            }
        }
    }
}
