using System;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>Singleton in the authoritative world while recording; see <see cref="ReplayRecorder"/>.</summary>
    public struct ReplayRecording : IComponentData, IDisposable
    {
        public const float DefaultSampleRate = 10f;
        public const float DefaultKeyframeInterval = 10f;

        public ReplayStream Stream;

        /// <summary>Each recorded entity's last sampled state, for change detection and its key.</summary>
        public NativeHashMap<Entity, ReplayEntity> Last;

        /// <summary>Entities seen by the sample being taken; the rest of <see cref="Last"/> were removed.</summary>
        public NativeHashSet<Entity> Seen;

        public float SampleInterval;
        public float KeyframeInterval;

        /// <summary>World time of the first sample; negative until it is taken.</summary>
        public double StartTime;
        public float NextSample;
        public float LastKeyframe;
        public int NextKey;

        public static ReplayRecording Create(float sampleRate = DefaultSampleRate,
            float keyframeInterval = DefaultKeyframeInterval) => new()
        {
            Stream = ReplayStream.Create(Allocator.Persistent),
            Last = new NativeHashMap<Entity, ReplayEntity>(256, Allocator.Persistent),
            Seen = new NativeHashSet<Entity>(256, Allocator.Persistent),
            SampleInterval = 1f / sampleRate,
            KeyframeInterval = keyframeInterval,
            StartTime = -1d,
        };

        public void Dispose()
        {
            Stream.Dispose();
            Last.Dispose();
            Seen.Dispose();
        }
    }
}
