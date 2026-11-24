using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Stages
{
    /// <summary>A tool page's stages of the source shown, each with its state, Log and Run, with Stop, the queue that runs them and their log.</summary>
    public sealed class StagesView<TSource> where TSource : class
    {
        private readonly Func<TSource, IEnumerable<Stage<TSource>>> _stages;
        private readonly PageShell _shell;
        private readonly VisualElement _list;
        private readonly Button _stop;
        private List<Stage<TSource>> _shown = new();

        /// <summary>Wires the <c>stages</c>, <c>stop</c> and log elements under <paramref name="root"/>; <paramref name="key"/> tells two sources apart.</summary>
        public StagesView(VisualElement root, PageShell shell, Func<TSource, IEnumerable<Stage<TSource>>> stages, Func<TSource, string> key)
        {
            _stages = stages;
            _shell = shell;
            _list = root.Require<VisualElement>("stages");
            Queue = new StageQueue<TSource>(key);
            Log = new StageLog<TSource>(root.Require<Label>("log-title"), root.Require<ScrollView>("log-scroll"), root.Require<Label>("log"));
            _stop = root.Require<Button>("stop");
            _stop.clicked += Queue.Stop;
            root.schedule.Execute(Poll).Every(200);
        }

        public StageQueue<TSource> Queue { get; }

        public StageLog<TSource> Log { get; }

        public TSource Current { get; private set; }

        /// <summary>Raised after a stage's Log button shows its log.</summary>
        public event Action LogPicked;

        /// <summary>Lists <paramref name="source"/>'s stages and shows the log shown before, or its newest.</summary>
        public void Show(TSource source)
        {
            Current = source;
            _shown = _stages(source).ToList();
            _list.Clear();
            foreach (var stage in _shown)
            {
                var row = new StageRow(stage.Title, stage.State(source));
                row.Log.clicked += () =>
                {
                    Log.Show(Current, stage, _shown);
                    LogPicked?.Invoke();
                };
                row.Run.clicked += () => Queue.Add(Current, stage);
                row.Run.SetEnabled(stage.CanRun(source));
                _list.Add(row.Root);
            }

            Log.Show(source, Log.Stage, _shown);
        }

        /// <summary>Queues every stage of the source shown that can run.</summary>
        public void RunAll()
        {
            foreach (var stage in _shown.Where(stage => stage.CanRun(Current)))
            {
                Queue.Add(Current, stage);
            }
        }

        /// <summary>Queues each source's stages from its first that is not Done on, since each reads what the ones before it make.</summary>
        public void RunOutOfDate(IEnumerable<TSource> sources)
        {
            foreach (var source in sources)
            {
                var stages = _stages(source).Where(stage => stage.CanRun(source));
                foreach (var stage in stages.SkipWhile(stage => stage.State(source) == StageState.Done))
                {
                    Queue.Add(source, stage);
                }
            }
        }

        // Follows the running stage's log while its source is shown.
        private void Poll()
        {
            _shell.ShowBusy(Queue.Busy);
            _stop.SetEnabled(Queue.Busy);
            if (Queue.Running is { } running && Queue.Same(running.Source, Current))
            {
                Log.Follow(running.Source, running.Stage);
            }
        }
    }
}
