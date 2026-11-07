using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// One step that (re)creates generated assets. Subclass it (engine or game) and it appears on the Build page's
    /// Generators tab; <see cref="GeneratorRunner.RunAll"/> runs every available one in <see cref="Order"/>.
    /// </summary>
    public abstract class Generator
    {
        public abstract string Name { get; }

        /// <summary>Heading the step is listed under (the engine or the game's name).</summary>
        public abstract string Group { get; }

        /// <summary>Run position, lowest first; steps that use another's output come after it.</summary>
        public abstract int Order { get; }

        /// <summary>One line on what the step makes.</summary>
        public abstract string Description { get; }

        /// <summary>What the step reads: paths, or a short description when they are many.</summary>
        public abstract string Sources { get; }

        /// <summary>Asset paths of the files or folders the step reads; a folder counts every file below it.</summary>
        public abstract IEnumerable<string> Inputs { get; }

        /// <summary>What the step writes: paths, or a short description when they are many. Never edit these by hand.</summary>
        public abstract string Outputs { get; }

        /// <summary>False hides the step, e.g. engine steps that write into a read-only package.</summary>
        public virtual bool Available => true;

        public abstract void Run();

        /// <summary>True when the step never ran here, failed last time, or an input changed since it ran.</summary>
        public bool IsOutOfDate(GeneratorRunLog log)
        {
            var last = log.Find(Name);
            return last == null || last.IsOlderThan(Inputs);
        }

        /// <summary>The path of this type's own script, for a step whose settings are constants in its code.</summary>
        protected string ScriptPath()
        {
            var name = GetType().Name;
            return AssetDatabase.FindAssets($"{name} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => System.IO.Path.GetFileNameWithoutExtension(path) == name)
                ?? throw new InvalidOperationException($"HyperRTS: the script of generator {name} is missing.");
        }
    }
}
