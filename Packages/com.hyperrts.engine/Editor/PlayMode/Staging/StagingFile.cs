using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>A scripted Play check read from a hand-written JSON file: a scene and timed steps.</summary>
    internal sealed class StagingFile
    {
        public const string Root = "Logs/Stagings";

        private StagingFile(string name, string scene, List<StagingStep> steps)
        {
            Name = name;
            Scene = scene;
            Steps = steps;
        }

        public string Name { get; }

        public string Scene { get; }

        /// <summary>In run order: by time, and in file order at the same time.</summary>
        public IReadOnlyList<StagingStep> Steps { get; }

        public string Folder => $"{Root}/{Name}";

        public static StagingFile Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"HyperRTS: no staging file at '{path}'.");
            }

            return Parse(File.ReadAllText(path), path);
        }

        /// <summary>Throws naming the file and the key for bad JSON, an unknown key, a missing or misplaced value or an undefined group.</summary>
        public static StagingFile Parse(string json, string source)
        {
            var root = StagingObject.Parse(json, source);
            var name = root.Text("name");
            var scene = root.Text("scene");
            var steps = root.Items("steps").Select(StagingStep.Read).OrderBy(step => step.At).ToList();
            root.Finish();
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw root.Problem($"has a 'name' of '{name}' that is not a folder name");
            }

            Check(steps, source);
            return new StagingFile(name, scene, steps);
        }

        // Every group a step uses is made by a step that runs before it; no group or output is made twice.
        private static void Check(List<StagingStep> steps, string source)
        {
            var groups = new HashSet<string>();
            var outputs = new HashSet<string>();
            foreach (var step in steps)
            {
                var where = $"HyperRTS: staging {source} step {step.Index} ({step.Kind})";
                foreach (var used in step.Uses.Where(used => !groups.Contains(used)))
                {
                    throw new InvalidOperationException($"{where} uses group '{used}', which no step makes before it.");
                }

                if (step.Defines != null && !groups.Add(step.Defines))
                {
                    throw new InvalidOperationException($"{where} makes group '{step.Defines}' a second time.");
                }

                if (step.Output != null && !outputs.Add(step.Output))
                {
                    throw new InvalidOperationException($"{where} writes '{step.Output}' a second time.");
                }
            }
        }
    }
}
