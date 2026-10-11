using System.IO;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A map scene built from a spec carries its last report's errors, and is flagged when it was never built.</summary>
    public sealed class MapSpecRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            foreach (var scene in scenes.Scenes)
            {
                if (string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                var spec = Path.ChangeExtension(scene.path, null) + MapPaths.SpecExtension;
                if (!File.Exists(spec))
                {
                    continue;
                }

                var name = MapPaths.NameOf(spec);
                var report = MapReport.Load(name);
                if (report == null)
                {
                    issues.Error(null, $"Map '{name}' has a spec but no report; build it with HyperRTS ▸ Maps ▸ Build.");
                    continue;
                }

                foreach (var error in report.errors)
                {
                    issues.Error(null, $"Map '{name}': {error}");
                }
            }
        }
    }
}
