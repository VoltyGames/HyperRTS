using System;
using HyperRTS.Simulation.Cues;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>The recorded samples as native lists, shared by the recorder and the playback.</summary>
    public struct ReplayStream : IDisposable
    {
        public NativeList<ReplayFrame> Frames;
        public NativeList<ReplayEntity> Entities;
        public NativeList<int> Removed;
        public NativeList<PackedCue> Cues;

        public static ReplayStream Create(Allocator allocator) => new()
        {
            Frames = new NativeList<ReplayFrame>(64, allocator),
            Entities = new NativeList<ReplayEntity>(1024, allocator),
            Removed = new NativeList<int>(64, allocator),
            Cues = new NativeList<PackedCue>(256, allocator),
        };

        public void Dispose()
        {
            Frames.Dispose();
            Entities.Dispose();
            Removed.Dispose();
            Cues.Dispose();
        }

        /// <summary>Cues the frames so far hold; those recorded after them belong to the next sample.</summary>
        public readonly int SampledCues
        {
            get
            {
                if (Frames.IsEmpty)
                {
                    return 0;
                }

                var last = Frames[Frames.Length - 1];
                return last.CueStart + last.CueCount;
            }
        }

        /// <summary>The first frame recorded after <paramref name="time"/>.</summary>
        public readonly int FrameAfter(float time) => time < Frames[0].Time ? 0 : FrameAt(time) + 1;

        /// <summary>Adds a frame's cues, or only its impacts, to <paramref name="queue"/>.</summary>
        public readonly void AddCues(int frame, DynamicBuffer<CueEvent> queue, bool impactsOnly)
        {
            var sample = Frames[frame];
            for (var i = sample.CueStart; i < sample.CueStart + sample.CueCount; i++)
            {
                if (impactsOnly && Cues[i].Slot != (byte)CueSlot.Impact)
                {
                    continue;
                }

                queue.Add(Cues[i].Unpack());
            }
        }

        /// <summary>The last frame at or before <paramref name="time"/> (the first frame before it starts).</summary>
        public readonly int FrameAt(float time)
        {
            int low = 0, high = Frames.Length - 1;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (Frames[mid].Time <= time)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        public readonly int KeyframeAtOrBefore(int frame)
        {
            while (frame > 0 && !Frames[frame].Keyframe)
            {
                frame--;
            }

            return frame;
        }

        /// <summary>Applies one frame to a key → state map; a keyframe replaces its contents.</summary>
        public readonly void Apply(int frame, NativeHashMap<int, ReplayEntity> state)
        {
            var sample = Frames[frame];
            if (sample.Keyframe)
            {
                state.Clear();
            }

            for (var i = sample.EntityStart; i < sample.EntityStart + sample.EntityCount; i++)
            {
                state[Entities[i].Key] = Entities[i];
            }

            for (var i = sample.RemovedStart; i < sample.RemovedStart + sample.RemovedCount; i++)
            {
                state.Remove(Removed[i]);
            }
        }
    }
}
