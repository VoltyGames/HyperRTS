using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Metrics;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>A build's result as <c>Logs/MapReports/&lt;Map&gt;.json</c> stores it, next to the overview image.</summary>
    [Serializable]
    public sealed class MapReport
    {
        public string map;
        public string status;
        public string built;
        public List<string> errors = new();
        public List<string> warnings = new();
        public MapMetrics metrics;

        public bool Passed => errors.Count == 0;

        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MapReports"));

        public static string JsonPath(string map) => Path.Combine(Folder, map + ".json");

        public static string ImagePath(string map) => Path.Combine(Folder, map + ".png");

        public static MapReport Create(MapSpec spec, MapIssues issues, MapMetrics metrics) => new()
        {
            map = spec.name,
            status = spec.status,
            built = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            errors = new List<string>(issues.Errors),
            warnings = new List<string>(issues.Warnings),
            metrics = metrics,
        };

        /// <summary>The last report written for a map, or null if it was never built.</summary>
        public static MapReport Load(string map)
        {
            var path = JsonPath(map);
            return File.Exists(path) ? JsonUtility.FromJson<MapReport>(File.ReadAllText(path)) : null;
        }

        public void Write(Texture2D overview)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(JsonPath(map), JsonUtility.ToJson(this, true));
            if (overview != null)
            {
                File.WriteAllBytes(ImagePath(map), overview.EncodeToPNG());
            }
        }
    }
}
