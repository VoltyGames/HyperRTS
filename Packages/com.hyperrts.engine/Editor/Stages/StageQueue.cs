using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Stages;

/// <summary>Runs a tool page's stages one at a time, in the order they were added.</summary>
public sealed class StageQueue<TSource>(Func<TSource, string> key) where TSource : class
{
    private readonly List<(TSource Source, Stage<TSource> Stage)> _waiting = [];

    /// <summary>Raised after each stage ends, with the source it ran for.</summary>
    public event Action<TSource> Finished;

    /// <summary>The stage running now and its source, or null.</summary>
    public (TSource Source, Stage<TSource> Stage)? Running { get; private set; }

    public bool Busy => Running != null || _waiting.Count > 0;

    /// <summary>True when <paramref name="a"/> and <paramref name="b"/> are the same source.</summary>
    public bool Same(TSource a, TSource b) => a != null && b != null && key(a) == key(b);

    /// <summary>Queues <paramref name="stage"/> for <paramref name="source"/> unless it is already waiting or running.</summary>
    public void Add(TSource source, Stage<TSource> stage)
    {
        var draining = Busy;
        if (Is(Running, source, stage) || _waiting.Any(item => Is(item, source, stage)))
        {
            return;
        }

        _waiting.Add((source, stage));
        if (!draining)
        {
            _ = Drain();
        }
    }

    /// <summary>Drops the waiting stages and ends the running one.</summary>
    public void Stop()
    {
        _waiting.Clear();
        Running?.Stage.Stop();
    }

    // A page lists new stage instances each time, so a stage is known by its title.
    private bool Is((TSource Source, Stage<TSource> Stage)? item, TSource source, Stage<TSource> stage) =>
        item is { } found && Same(found.Source, source) && found.Stage.Title == stage.Title;

    private async Task Drain()
    {
        while (_waiting.Count > 0)
        {
            Running = _waiting[0];
            _waiting.RemoveAt(0);
            var (source, stage) = Running.Value;
            await RunOne(source, stage);
            Running = null;
            AssetDatabase.Refresh();
            Finished?.Invoke(source);
        }
    }

    // A failed stage drops its source's later stages, which read what it makes.
    private async Task RunOne(TSource source, Stage<TSource> stage)
    {
        try
        {
            await stage.Run(source);
        }
        catch (Exception exception)
        {
            _waiting.RemoveAll(item => Same(item.Source, source));
            Debug.LogException(exception);
        }
    }
}
