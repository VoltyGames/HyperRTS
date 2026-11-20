using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common.Json;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>A scripted Play check read from a hand-written JSON file named for it: a scene and timed steps.</summary>
    internal sealed class StagingFile
    {
        public const string Root = "Logs/Stagings";

        private StagingFile(string name, string scene, Vector2Int resolution, List<StagingStep> steps)
        {
            Name = name;
            Scene = scene;
            Resolution = resolution;
            Steps = steps;
        }

        public string Name { get; }

        public string Scene { get; }

        /// <summary>The Game view size every capture records at, in pixels; zero keeps the Game view's own.</summary>
        public Vector2Int Resolution { get; }

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

        /// <summary>
        /// The staging's name is <paramref name="source"/>'s file name without its extension. Throws naming the file and
        /// the key for bad JSON, an unknown key, a missing or misplaced value or an undefined group.
        /// </summary>
        public static StagingFile Parse(string json, string source)
        {
            var name = Path.GetFileNameWithoutExtension(source);
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException($"HyperRTS: staging {source} is named '{name}', which is not a folder name.");
            }

            var spec = StrictJson.Read<StagingSpec>(json, $"staging {source}", true);
            var scene = spec.scene ?? throw new InvalidOperationException($"HyperRTS: staging {source} needs 'scene'.");
            var entries = spec.steps ?? throw new InvalidOperationException($"HyperRTS: staging {source} needs 'steps'.");
            var steps = entries.Select((entry, i) => Build(entry, i, source)).OrderBy(step => step.At).ToList();
            Check(steps, source);
            return new StagingFile(name, scene, ReadResolution(spec.resolution, source), steps);
        }

        private static StagingStep Build(StagingEntry entry, int index, string source)
        {
            var where = $"steps[{index}]";
            try
            {
                if (entry == null)
                {
                    throw new InvalidOperationException("must be an object");
                }

                if (entry.at < 0f)
                {
                    throw new InvalidOperationException($"has a negative 'at' of {entry.at}");
                }

                var (kind, step) = entry.Chosen();
                where += $".{kind}";
                step.Check();
                return step.Placed(index, entry.at, kind);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException($"HyperRTS: staging {source} {where} {exception.Message}.", exception);
            }
        }

        private static Vector2Int ReadResolution(float[] size, string source)
        {
            if (size == null)
            {
                return Vector2Int.zero;
            }

            var problem = new InvalidOperationException($"HyperRTS: staging {source} has a 'resolution' that is not [width, height] in whole pixels.");
            if (size.Length != 2)
            {
                throw problem;
            }

            var pixels = new Vector2Int((int)size[0], (int)size[1]);
            if (pixels.x < 0 || pixels.y < 0 || pixels.x != size[0] || pixels.y != size[1])
            {
                throw problem;
            }

            return pixels;
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
