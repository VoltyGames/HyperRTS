using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.Stages;

namespace HyperRTS.Editor.Effects.Stages
{
    /// <summary>
    /// A step the Effects page runs for a rig, with its state, log and Run button. The engine's are the graph build and
    /// the capture; a game adds its own, such as making the rig's sheets, by subclassing, and every subclass is found.
    /// </summary>
    public abstract class EffectRigStage
    {
        public const string LogRoot = "Logs/Effects";

        /// <summary>Where the engine's stages sit in <see cref="Order"/>; a game's stage that makes what they read comes first.</summary>
        public const int GraphOrder = 200, CaptureOrder = 300;

        /// <summary>The stages that apply to <paramref name="rig"/>, in the order they run.</summary>
        public static List<EffectRigStage> For(EffectRigEntry rig) =>
            TypeDiscovery.Instances<EffectRigStage>().Where(stage => stage.Applies(rig)).OrderBy(stage => stage.Order).ToList();

        /// <summary>One word, which also names the stage's log file.</summary>
        public abstract string Title { get; }

        /// <summary>Where the stage sits in a rig's list: the order the page runs them in.</summary>
        public abstract int Order { get; }

        public abstract bool Applies(EffectRigEntry rig);

        public abstract StageState State(EffectRigEntry rig);

        /// <summary>The project-relative images the stage made, shown under the rig's stages.</summary>
        public virtual IReadOnlyList<string> Sheet(EffectRigEntry rig) => Array.Empty<string>();

        /// <summary>
        /// Runs the stage for <paramref name="rig"/>, writing its log as it goes or at the end, and throws when it fails,
        /// which drops the rig's later stages.
        /// </summary>
        public abstract Task Run(EffectRigEntry rig);

        /// <summary>Ends a run that is still going; a stage that runs in one call has nothing to end.</summary>
        public virtual void Stop()
        {
        }

        /// <summary>
        /// Runs <paramref name="work"/>, which ends within the Editor, and writes the lines it returns to the log, or its
        /// error with exit code 1 before throwing it again.
        /// </summary>
        protected async Task Record(EffectRigEntry rig, Func<Task<string>> work)
        {
            var log = Log(rig);
            try
            {
                log.Write(await work(), 0);
            }
            catch (Exception exception)
            {
                log.Write(exception.Message, 1);
                throw;
            }
        }

        /// <summary>The stage's log for <paramref name="rig"/>, under <see cref="LogRoot"/>.</summary>
        public StageLogFile Log(EffectRigEntry rig) => new($"{LogRoot}/{rig.Name}/{Title.ToLowerInvariant()}.log");
    }
}
