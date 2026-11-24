using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HyperRTS.Editor.Stages
{
    /// <summary>One step a tool page runs for a source, such as a model recipe or an effect rig, with its state, log and sheets.</summary>
    public abstract class Stage<TSource> where TSource : class
    {
        protected Stage(string title)
        {
            Title = title;
        }

        /// <summary>One word, which also names the stage's log file.</summary>
        public string Title { get; }

        public virtual bool CanRun(TSource source) => true;

        public abstract StageState State(TSource source);

        /// <summary>The project-relative images the stage made.</summary>
        public virtual IReadOnlyList<string> Sheets(TSource source) => Array.Empty<string>();

        /// <summary>Runs the stage, and throws when it fails, which drops the source's later stages.</summary>
        public abstract Task Run(TSource source);

        /// <summary>Ends a run that is still going; a stage that runs in one call has nothing to end.</summary>
        public virtual void Stop()
        {
        }

        /// <summary>The stage's log file, or null for a stage that keeps its runs elsewhere.</summary>
        public virtual StageLogFile Log(TSource source) => null;

        /// <summary>When the stage last logged a run, or null before its first.</summary>
        public virtual DateTime? LoggedUtc(TSource source) => Log(source) is { Exists: true } log ? log.WrittenUtc : null;

        /// <summary>The text of the stage's last run, read while it may still be writing it.</summary>
        public virtual string LogText(TSource source) => Log(source) is { Exists: true } log ? log.Text() : "";
    }
}
