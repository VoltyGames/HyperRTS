using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Stages;
using UnityEngine;

namespace HyperRTS.Editor.Generators;

/// <summary>A stage that runs generator items in the Editor, taking its state and log from the generator run log.</summary>
public abstract class GeneratorStage<TSource>(string title) : Stage<TSource>(title) where TSource : class
{
    /// <summary>The generator items the stage runs.</summary>
    protected abstract (Generator Generator, IReadOnlyList<GeneratorItem> Items) Items(TSource source);

    // The generator run log keeps each error and the console shows it, so a failed item drops no later stage.
    public override Task Run(TSource source)
    {
        var (generator, items) = Items(source);
        try
        {
            GeneratorRunner.RunItems(generator, items);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        return Task.CompletedTask;
    }

    // The worst state of the items.
    public override StageState State(TSource source)
    {
        var (generator, items) = Items(source);
        var log = GeneratorRunLog.Load();
        return items.Select(item => generator.State(item, log)).DefaultIfEmpty(StageState.Done).Max();
    }

    public override DateTime? LoggedUtc(TSource source)
    {
        var runs = LastRuns(source);
        return runs.Count == 0 ? null : runs.Max(run => run.RanUtc);
    }

    public override string LogText(TSource source)
    {
        var runs = LastRuns(source);
        return string.Join("\n", runs.Select(run => runs.Count == 1 ? run.ToString() : $"{run.item}: {run}"));
    }

    // The last run of each of the stage's items that ran here.
    private List<GeneratorRun> LastRuns(TSource source)
    {
        var (generator, items) = Items(source);
        var log = GeneratorRunLog.Load();
        return items.Select(item => log.Find(generator.Name, item.Name)).Where(run => run != null).ToList();
    }
}
