using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// When each generator last ran in this project and how it ended, kept in <c>Library/</c> so it stays on this
    /// machine.
    /// </summary>
    [Serializable]
    public sealed class GeneratorRunLog
    {
        private const string Folder = "Library/HyperRTS";
        private const string FilePath = Folder + "/GeneratorRuns.json";

        [SerializeField] private List<GeneratorRun> runs = new();

        public static GeneratorRunLog Load()
        {
            if (!File.Exists(FilePath))
            {
                return new GeneratorRunLog();
            }

            return JsonUtility.FromJson<GeneratorRunLog>(File.ReadAllText(FilePath)) ?? new GeneratorRunLog();
        }

        /// <summary>The generator's last run, or null when it never ran here.</summary>
        public GeneratorRun Find(string generator) => runs.Find(run => run.generator == generator);

        /// <summary>Records a run that ended now; <paramref name="error"/> is null when it succeeded.</summary>
        public static void Record(string generator, string error)
        {
            var log = Load();
            log.runs.RemoveAll(run => run.generator == generator);
            log.runs.Add(new GeneratorRun
            {
                generator = generator,
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                error = error,
            });
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonUtility.ToJson(log, true));
        }
    }
}
