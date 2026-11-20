namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Work a step starts that goes on over frames, such as a capture or a sampler.</summary>
    internal abstract class StagingActivity
    {
        /// <summary>True for a Recorder capture; only one runs at a time.</summary>
        public virtual bool Records => false;

        /// <summary>Called once a frame; true when the work is done. Throwing fails the step.</summary>
        public abstract bool Advance(StagingRun run);

        /// <summary>Releases what the activity holds when the run ends before it is done.</summary>
        public virtual void Stop()
        {
        }
    }
}
