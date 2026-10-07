using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Match;
using Unity.Collections;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>A recorded match: header plus the sample stream, as saved by <see cref="ReplaySerializer"/>.</summary>
    public sealed class Replay
    {
        public string ScenePath = "";

        /// <summary>Game data the engine stores but never reads (a JSON of map id, settings...).</summary>
        public string Metadata = "";
        public float SampleRate;
        public float Duration;
        public List<ReplayPlayerInfo> Players = new();

        /// <summary>The outcome when the recording was taken; still <see cref="MatchPhase.Playing"/> if unfinished.</summary>
        public MatchState Result;

        public ReplayFrame[] Frames = Array.Empty<ReplayFrame>();
        public ReplayEntity[] Entities = Array.Empty<ReplayEntity>();
        public int[] Removed = Array.Empty<int>();

        /// <summary>Copies the samples into a native stream the caller disposes.</summary>
        public ReplayStream ToStream(Allocator allocator)
        {
            var stream = ReplayStream.Create(allocator);
            Append(stream.Frames, Frames);
            Append(stream.Entities, Entities);
            Append(stream.Removed, Removed);
            return stream;
        }

        /// <summary>Takes a copy of <paramref name="stream"/>'s samples.</summary>
        public void CopySamples(in ReplayStream stream)
        {
            Frames = stream.Frames.AsArray().ToArray();
            Entities = stream.Entities.AsArray().ToArray();
            Removed = stream.Removed.AsArray().ToArray();
            Duration = Frames.Length == 0 ? 0f : Frames[^1].Time;
        }

        private static void Append<T>(NativeList<T> list, T[] items) where T : unmanaged
        {
            using var copy = new NativeArray<T>(items, Allocator.Temp);
            list.AddRange(copy);
        }
    }
}
