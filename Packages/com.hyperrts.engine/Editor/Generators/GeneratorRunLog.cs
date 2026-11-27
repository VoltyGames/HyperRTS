using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Generators;

/// <summary>
/// When each generator, or each of its items, last ran in this project and how it ended, kept in <c>Library/</c>
/// so it stays on this machine.
/// </summary>
[Serializable]
public sealed class GeneratorRunLog
{
    private const string Folder = "Library/HyperRTS";
    private const string FilePath = Folder + "/GeneratorRuns.json";

    [SerializeField] private List<GeneratorRun> runs = [];

    /// <summary>Raised after every recorded run, so views that cache states read them again.</summary>
    [NoAutoStaticsCleanup]
    public static event Action Recorded;

    // The parsed file and when it was written, so a write by another process still shows.
    [NoAutoStaticsCleanup]
    private static GeneratorRunLog _loaded;

    [NoAutoStaticsCleanup]
    private static DateTime _loadedWritten;

    /// <summary>The log on disk, parsed again only after the file changed; treat it as read-only.</summary>
    public static GeneratorRunLog Load()
    {
        if (!File.Exists(FilePath))
        {
            _loaded = null;
            return new GeneratorRunLog();
        }

        var written = File.GetLastWriteTimeUtc(FilePath);
        if (_loaded == null || written != _loadedWritten)
        {
            _loaded = Read();
            _loadedWritten = written;
        }

        return _loaded;
    }

    private static GeneratorRunLog Read()
    {
        try
        {
            return JsonUtility.FromJson<GeneratorRunLog>(File.ReadAllText(FilePath))
                ?? throw new InvalidDataException($"{FilePath} is empty.");
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"{FilePath} is not valid JSON: {e.Message}", e);
        }
    }

    /// <summary>The last run of the generator's item, or of the whole generator when <paramref name="item"/> is empty; null when it never ran here.</summary>
    public GeneratorRun Find(string generator, string item) => runs.Find(run => run.Matches(generator, item));

    /// <summary>Keeps <paramref name="run"/> in place of any earlier run of the same generator and item.</summary>
    public void Add(GeneratorRun run)
    {
        runs.RemoveAll(earlier => earlier.Matches(run.generator, run.item));
        runs.Add(run);
    }

    /// <summary>Records a run that ended now; <paramref name="item"/> is empty for a whole generator and <paramref name="error"/> null when it succeeded.</summary>
    public static void Record(string generator, string item, string error)
    {
        var log = File.Exists(FilePath) ? Read() : new GeneratorRunLog();
        log.Add(new GeneratorRun
        {
            generator = generator,
            item = item,
            utcTicks = DateTime.UtcNow.Ticks,
            error = error,
        });
        Directory.CreateDirectory(Folder);
        _loaded = null;
        File.WriteAllText(FilePath, JsonUtility.ToJson(log, true));
        Recorded?.Invoke();
    }
}
