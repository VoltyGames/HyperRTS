using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;

namespace HyperRTS.Editor.Generators;

/// <summary>The last run of one generator, or of one of its items: its UTC time and, when it failed, why.</summary>
[Serializable]
public sealed class GeneratorRun
{
    public string generator;

    /// <summary>The <see cref="GeneratorItem"/>'s name, empty for a generator that runs whole.</summary>
    public string item;

    public long utcTicks;
    public string error;

    public bool Failed => !string.IsNullOrEmpty(error);

    public DateTime RanUtc => new(utcTicks, DateTimeKind.Utc);

    /// <summary>When the run ended, as local time for display.</summary>
    public string RanText => RanUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public bool Matches(string generatorName, string itemName) => generator == generatorName && item == itemName;

    /// <summary>True when the run failed or any file under <paramref name="inputs"/> was written after it.</summary>
    public bool IsOlderThan(IEnumerable<string> inputs)
    {
        var ran = RanUtc;
        return Failed || inputs.SelectMany(Files).Any(file => File.GetLastWriteTimeUtc(file) > ran);
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

    public override string ToString() => Failed ? $"Failed {RanText}: {error}" : $"Ran {RanText}";
}
