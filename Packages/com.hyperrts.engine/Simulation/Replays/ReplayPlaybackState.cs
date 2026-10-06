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

        /// <summary><see cref="Common.EntityInfo.TypeId"/> → baked prefab, for later spawns.</summary>
        public NativeHashMap<int, Entity> Prefabs;

        /// <summary>Types without a baked prefab, already reported.</summary>
        public NativeHashSet<int> MissingTypes;

        /// <summary>Frame shown now; negative until the scene's entities are taken over.</summary>
        public int Index;

        public static ReplayPlaybackState Create(Replay replay) => new()
        {
            Stream = replay.ToStream(Allocator.Persistent),
            From = new NativeHashMap<int, ReplayEntity>(256, Allocator.Persistent),
            To = new NativeHashMap<int, ReplayEntity>(256, Allocator.Persistent),
            Live = new NativeHashMap<int, Entity>(256, Allocator.Persistent),
            Prefabs = new NativeHashMap<int, Entity>(64, Allocator.Persistent),
            MissingTypes = new NativeHashSet<int>(8, Allocator.Persistent),
            Index = -1,
        };

        public void Dispose()
        {
            Stream.Dispose();
            From.Dispose();
            To.Dispose();
            Live.Dispose();
            Prefabs.Dispose();
            MissingTypes.Dispose();
        }
    }
}
