using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Stages
{
    /// <summary>A tool page's log of one stage: its last run, or the text of its run going on now, kept to its last 100000 characters.</summary>
    public sealed class StageLog<TSource> where TSource : class
    {
        private const int MaxLength = 100000;

        private readonly Label _title;
        private readonly ScrollView _scroll;
        private readonly Label _text;
        private int _shownLength;

        public StageLog(Label title, ScrollView scroll, Label text)
        {
            _title = title;
            _scroll = scroll;
            _text = text;
            _text.selection.isSelectable = true;
        }

        /// <summary>The stage shown, or null before any has run.</summary>
        public Stage<TSource> Stage { get; private set; }

        /// <summary>Shows <paramref name="stage"/>'s last run, or the one logged last of <paramref name="stages"/> when it is null.</summary>
        public void Show(TSource source, Stage<TSource> stage, IReadOnlyList<Stage<TSource>> stages)
        {
            Stage = stage == null ? Latest(source, stages) : stages.FirstOrDefault(listed => listed.Title == stage.Title);
            if (Stage == null)
            {
                _title.text = "No stage has run yet.";
                Write("");
                return;
            }

            var logged = Stage.LoggedUtc(source);
            _title.text = logged is { } time ? $"{Stage.Title}, {time.ToLocalTime():g}" : $"{Stage.Title} has not run yet.";
            Write(Stage.LogText(source));
        }

        /// <summary>Shows the text <paramref name="stage"/> has written so far in its run going on now.</summary>
        public void Follow(TSource source, Stage<TSource> stage)
        {
            Stage = stage;
            _title.text = $"{stage.Title}, running";
            var text = stage.LogText(source);
            if (text.Length != _shownLength)
            {
                Write(text);
            }
        }

        private static Stage<TSource> Latest(TSource source, IEnumerable<Stage<TSource>> stages) => stages
            .Select(stage => (Stage: stage, Logged: stage.LoggedUtc(source)))
            .Where(pair => pair.Logged != null)
            .OrderByDescending(pair => pair.Logged)
            .Select(pair => pair.Stage)
            .FirstOrDefault();

        private void Write(string text)
        {
            _shownLength = text.Length;
            _text.text = text.Length > MaxLength ? text[^MaxLength..] : text;
            _scroll.schedule.Execute(() => _scroll.verticalScroller.value = _scroll.verticalScroller.highValue);
        }
    }
}
