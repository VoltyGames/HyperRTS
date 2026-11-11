using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>A map scene built from a spec carries its last report's errors, and is flagged when it was never built.</summary>
    public sealed class MapSpecRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            foreach (var scene in scenes.Scenes)
            {
                var spec = MapPaths.SpecBeside(scene.path);
                if (spec == null)
                {
                    continue;
                }

                var name = MapPaths.MapName(spec);
                var report = MapReport.Load(name);
                if (report == null)
                {
                    issues.Error(null, $"Map '{name}' has a spec but no report; build it from the Map Editor or the map command line.");
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
