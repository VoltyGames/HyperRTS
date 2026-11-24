using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Stages;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Stages
{
    /// <summary>
    /// The Effects page's stages of the shown rig, each with its state, Log and Run, the queue that runs them one at a
    /// time, the log of the stage picked or running, and the images the stages made.
    /// </summary>
    internal sealed class EffectStagesView
    {
        private const int PollMilliseconds = 200;
        private const int MaxLogLength = 100000;

        private readonly List<(EffectRigEntry Rig, EffectRigStage Stage)> _waiting = new();
        private readonly List<Texture2D> _images = new();
        private readonly PageShell _shell;
        private readonly VisualElement _list;
        private readonly VisualElement _sheet;
        private readonly Label _logTitle;
        private readonly ScrollView _logScroll;
        private readonly Label _log;
        private readonly Button _stop;
        private EffectRigEntry _rig;
        private List<EffectRigStage> _stages = new();
        private EffectRigStage _logStage;
        private (EffectRigEntry Rig, EffectRigStage Stage)? _running;
        private bool _draining;
        private int _shownLength;

        /// <summary>Wires the <c>stages</c>, <c>run-all</c>, <c>stop</c>, <c>sheet</c> and log elements under <paramref name="root"/>.</summary>
        public EffectStagesView(VisualElement root, PageShell shell)
        {
            _shell = shell;
            _list = root.Require<VisualElement>("stages");
            _sheet = root.Require<VisualElement>("sheet");
            _logTitle = root.Require<Label>("log-title");
            _logScroll = root.Require<ScrollView>("log-scroll");
            _log = root.Require<Label>("log");
            _log.selection.isSelectable = true;
            _stop = root.Require<Button>("stop");
            _stop.clicked += Stop;
            root.Require<Button>("run-all").clicked += RunOutOfDate;
            root.schedule.Execute(Poll).Every(PollMilliseconds);
        }

        /// <summary>Raised after each stage ends, for the rig it ran on.</summary>
        public event Action<EffectRigEntry> Finished;

        /// <summary>Lists <paramref name="rig"/>'s stages with the log shown before, or the newest, and their images.</summary>
        public void Show(EffectRigEntry rig)
        {
            _rig = rig;
            _stages = EffectRigStage.For(rig);
            _list.Clear();
            foreach (var stage in _stages)
            {
                var row = new StageRow(stage.Title, stage.State(rig));
                row.Log.clicked += () => ShowLog(stage);
                row.Run.clicked += () => Add(_rig, stage);
                _list.Add(row.Root);
            }

            var logStage = _stages.FirstOrDefault(stage => Same(stage, _logStage));
            ShowLog(logStage ?? NewestLog());
            ShowSheet();
        }

        /// <summary>Queues <paramref name="stage"/> for <paramref name="rig"/> unless it is already waiting or running.</summary>
        public void Add(EffectRigEntry rig, EffectRigStage stage)
        {
            if (Is(_running, rig, stage) || _waiting.Any(item => Is(item, rig, stage)))
            {
                return;
            }

            _waiting.Add((rig, stage));
            if (!_draining)
            {
                _ = Drain();
            }
        }

        private static bool Is((EffectRigEntry Rig, EffectRigStage Stage)? item, EffectRigEntry rig, EffectRigStage stage) =>
            item is { } found && found.Rig.Path == rig.Path && Same(found.Stage, stage);

        // Each listing makes new stage instances, so a stage is known by its type.
        private static bool Same(EffectRigStage stage, EffectRigStage other) => stage?.GetType() == other?.GetType();

        // Every stage after the first one not Done runs too, since each reads what the ones before it make.
        private void RunOutOfDate()
        {
            foreach (var stage in _stages.SkipWhile(stage => stage.State(_rig) == StageState.Done))
            {
                Add(_rig, stage);
            }
        }

        private void Stop()
        {
            _waiting.Clear();
            _running?.Stage.Stop();
        }

        private async Task Drain()
        {
            _draining = true;
            while (_waiting.Count > 0)
            {
                var (rig, stage) = _waiting[0];
                _waiting.RemoveAt(0);
                _running = (rig, stage);
                if (rig.Path == _rig?.Path)
                {
                    ShowLog(stage);
                }

                await RunOne(rig, stage);
                _running = null;
                AssetDatabase.Refresh();
                Finished?.Invoke(rig);
            }

            _draining = false;
        }

        // A failed stage drops its rig's later stages, which read what it makes.
        private async Task RunOne(EffectRigEntry rig, EffectRigStage stage)
        {
            try
            {
                await stage.Run(rig);
            }
            catch (Exception exception)
            {
                _waiting.RemoveAll(item => item.Rig.Path == rig.Path);
                Debug.LogException(exception);
            }
        }

        private EffectRigStage NewestLog() => _stages
            .Where(stage => stage.Log(_rig).Exists)
            .OrderByDescending(stage => stage.Log(_rig).WrittenUtc)
            .FirstOrDefault();

        private void ShowLog(EffectRigStage stage)
        {
            _logStage = stage;
            if (stage == null)
            {
                _logTitle.text = "No stage has run yet.";
                Write("");
                return;
            }

            var log = stage.Log(_rig);
            _logTitle.text = log.Exists ? $"{stage.Title}, {log.WrittenUtc.ToLocalTime():g}" : $"{stage.Title} has not run yet.";
            Write(log.Exists ? log.Text() : "");
        }

        private void Write(string text)
        {
            _shownLength = text.Length;
            _log.text = text.Length > MaxLogLength ? text[^MaxLogLength..] : text;
            _logScroll.schedule.Execute(() => _logScroll.verticalScroller.value = _logScroll.verticalScroller.highValue);
        }

        private void ShowSheet()
        {
            _images.ForEach(Object.DestroyImmediate);
            _images.Clear();
            _sheet.Clear();
            foreach (var path in _stages.SelectMany(stage => stage.Sheet(_rig)).Where(File.Exists))
            {
                var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                texture.LoadImage(File.ReadAllBytes(path));
                _images.Add(texture);
                var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, tooltip = Path.GetFileName(path) };
                image.AddToClassList("hrts-effects__sheet-image");
                _sheet.Add(image);
            }

            _sheet.SetHidden(_images.Count == 0);
        }

        private bool IsShown((EffectRigEntry Rig, EffectRigStage Stage) item) =>
            item.Rig.Path == _rig?.Path && Same(item.Stage, _logStage);

        // Follows the running stage's log while it is the one shown.
        private void Poll()
        {
            var busy = _running != null || _waiting.Count > 0;
            _shell.ShowBusy(busy);
            _stop.SetEnabled(busy);
            if (_running is not { } running || !IsShown(running))
            {
                return;
            }

            var log = running.Stage.Log(running.Rig);
            _logTitle.text = $"{running.Stage.Title}, running";
            var text = log.Exists ? log.Text() : "";
            if (text.Length != _shownLength)
            {
                Write(text);
            }
        }
    }
}
