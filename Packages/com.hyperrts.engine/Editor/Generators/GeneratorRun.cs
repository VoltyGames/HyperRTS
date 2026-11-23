using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;

namespace HyperRTS.Editor.Generators
{
    /// <summary>The last run of one generator, or of one of its items: its local time and, when it failed, why.</summary>
    [Serializable]
    public sealed class GeneratorRun
    {
        public string generator;

        /// <summary>The <see cref="GeneratorItem"/>'s name, empty for a generator that runs whole.</summary>
        public string item;

        public string time;
        public string error;

        public const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

        public bool Failed => !string.IsNullOrEmpty(error);

        public bool Matches(string generatorName, string itemName) => generator == generatorName && item == itemName;

        /// <summary>True when the run failed or any file under <paramref name="inputs"/> was written after it.</summary>
        public bool IsOlderThan(IEnumerable<string> inputs)
        {
            var ran = DateTime.ParseExact(time, TimeFormat, CultureInfo.InvariantCulture);
            return Failed || inputs.SelectMany(Files).Any(file => File.GetLastWriteTime(file) > ran);
        }

        private static IEnumerable<string> Files(string assetPath)
        {
            var package = PackageInfo.FindForAssetPath(assetPath);
            var disk = package == null ? assetPath : package.resolvedPath + assetPath.Substring(package.assetPath.Length);
            if (File.Exists(disk))
            {
                return new[] { disk };
            }

            return Directory.Exists(disk)
                ? Directory.EnumerateFiles(disk, "*", SearchOption.AllDirectories).Where(file => !file.EndsWith(".meta"))
                : Enumerable.Empty<string>();
        }

        public override string ToString() => Failed ? $"Failed {time}: {error}" : $"Ran {time}";
    }
}
