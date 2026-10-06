using System;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>Playback bookkeeping beside <see cref="ReplayPlayback"/>: samples, and which entity shows each key.</summary>
    public struct ReplayPlaybackState : IComponentData, IDisposable
    {
        public ReplayStream Stream;

        /// <summary>Recorded state at frame <see cref="Index"/>.</summary>
        public NativeHashMap<int, ReplayEntity> From;

        /// <summary>Recorded state at the frame after <see cref="Index"/>, interpolated towards.</summary>
        public NativeHashMap<int, ReplayEntity> To;

        /// <summary>Recorded key → the entity showing it.</summary>
        public NativeHashMap<int, Entity> Live;

        /// <summary>Types without a baked prefab, already reported.</summary>
        public NativeHashSet<int> MissingTypes;

        /// <summary>Frame shown now; negative until the scene's entities are taken over.</summary>
        public int Index;

        /// <summary>Which gameplay phase groups were on before playback paused them, restored when it stops.</summary>
        public ulong GameplayEnabled;

        public static ReplayPlaybackState Create(Replay replay) => new()
        {
            Stream = replay.ToStream(Allocator.Persistent),
            From = new NativeHashMap<int, ReplayEntity>(256, Allocator.Persistent),
            To = new NativeHashMap<int, ReplayEntity>(256, Allocator.Persistent),
            Live = new NativeHashMap<int, Entity>(256, Allocator.Persistent),
            MissingTypes = new NativeHashSet<int>(8, Allocator.Persistent),
            Index = -1,
        };

        /// <summary>
        /// Moves <see cref="From"/> to frame <paramref name="index"/> and <see cref="To"/> to the frame after: rebuilds
        /// from a keyframe when going back or past one, otherwise steps through the deltas.
        /// </summary>
        public void Seek(int index)
        {
            var keyframe = Stream.KeyframeAtOrBefore(index);
            if (index < Index || keyframe > Index)
            {
                Index = keyframe;
                Stream.Apply(keyframe, From);
            }

            while (Index < index)
            {
                Index++;
                Stream.Apply(Index, From);
            }

            To.Clear();
            foreach (var pair in From)
            {
                To[pair.Key] = pair.Value;
            }

            if (index + 1 < Stream.Frames.Length)
            {
                Stream.Apply(index + 1, To);
            }
        }

        public void Dispose()
        {
            Stream.Dispose();
            From.Dispose();
            To.Dispose();
            Live.Dispose();
            MissingTypes.Dispose();
        }
    }
}
