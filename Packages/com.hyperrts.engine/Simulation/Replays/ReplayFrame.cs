namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// One sample: a range of <see cref="ReplayStream.Entities"/> and <see cref="ReplayStream.Removed"/>. A keyframe
    /// lists every entity alive; a delta only those that spawned or changed, plus the keys removed since. Its
    /// <see cref="ReplayStream.Cues"/> are the cues played since the previous sample.
    /// </summary>
    public struct ReplayFrame
    {
        /// <summary>Seconds since recording started.</summary>
        public float Time;
        public bool Keyframe;
        public int EntityStart;
        public int EntityCount;
        public int RemovedStart;
        public int RemovedCount;
        public int CueStart;
        public int CueCount;
    }
}
