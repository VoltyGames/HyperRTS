using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Match;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>Reads and writes <see cref="Replay"/> files: a GZip stream of a small header and the samples.</summary>
    public static class ReplaySerializer
    {
        private const int Magic = 0x4C505248; // "HRPL"

        /// <summary>Stays 1 until the first release; files of any other version are rejected.</summary>
        public const int Version = 1;

        /// <summary>Most elements reserved before any are read; larger lists grow as data arrives.</summary>
        private const int PreallocateLimit = 4096;

        public static void Save(Replay replay, string path)
        {
            using var file = File.Create(path);
            Write(replay, file);
        }

        /// <summary>Only the header (players, result, metadata): cheap enough to list a folder of replays.</summary>
        public static Replay LoadHeader(string path)
        {
            using var file = File.OpenRead(path);
            using var zip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new BinaryReader(zip);
            try
            {
                return ReadHeader(reader);
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("The replay file is truncated.", exception);
            }
        }

        public static Replay Load(string path)
        {
            using var file = File.OpenRead(path);
            return Read(file);
        }

        public static void Write(Replay replay, Stream stream)
        {
            using var zip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
            using var writer = new BinaryWriter(zip);
            WriteHeader(writer, replay);
            writer.Write(replay.Frames.Length);
            foreach (var frame in replay.Frames)
            {
                writer.Write(frame.Time);
                writer.Write(frame.Keyframe);
                writer.Write(frame.EntityCount);
                writer.Write(frame.RemovedCount);
                writer.Write(frame.CueCount);
            }

            WriteAll(writer, replay.Entities);
            WriteAll(writer, replay.Removed);
            WriteAll(writer, replay.Cues);
        }

        /// <summary>Throws <see cref="InvalidDataException"/> for files that aren't replays, are truncated or corrupt.</summary>
        public static Replay Read(Stream stream)
        {
            using var zip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var reader = new BinaryReader(zip);
            try
            {
                var replay = ReadHeader(reader);
                ReadSamples(reader, replay);
                return replay;
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("The replay file is truncated.", exception);
            }
        }

        /// <summary>
        /// Arrays grow as samples actually arrive rather than being sized up front from the counts, so a corrupt count
        /// fails on the missing data instead of allocating it.
        /// </summary>
        private static void ReadSamples(BinaryReader reader, Replay replay)
        {
            var frameCount = ReadCount(reader, "frames");
            var frames = new List<ReplayFrame>(Capacity(frameCount));
            long entities = 0, removed = 0, cues = 0;
            for (var i = 0; i < frameCount; i++)
            {
                var frame = new ReplayFrame { Time = reader.ReadSingle(), Keyframe = reader.ReadBoolean() };
                frame.EntityStart = (int)entities;
                frame.EntityCount = ReadCount(reader, "entity samples");
                frame.RemovedStart = (int)removed;
                frame.RemovedCount = ReadCount(reader, "removals");
                frame.CueStart = (int)cues;
                frame.CueCount = ReadCount(reader, "cues");
                entities = Total(entities + frame.EntityCount, "entity samples");
                removed = Total(removed + frame.RemovedCount, "removals");
                cues = Total(cues + frame.CueCount, "cues");
                frames.Add(frame);
            }

            replay.Frames = frames.ToArray();
            replay.Entities = ReadAll<ReplayEntity>(reader, (int)entities);
            replay.Removed = ReadAll<int>(reader, (int)removed);
            replay.Cues = ReadAll<PackedCue>(reader, (int)cues);
        }

        private static int ReadCount(BinaryReader reader, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException($"Corrupt replay: {count} {what}.");
            }

            return count;
        }

        private static long Total(long total, string what)
        {
            if (total > int.MaxValue)
            {
                throw new InvalidDataException($"Corrupt replay: more {what} than a replay can hold.");
            }

            return total;
        }

        // Packed structs go out as their raw bytes, so a new field changes the file without a second field list.
        private static void WriteAll<T>(BinaryWriter writer, T[] items) where T : unmanaged =>
            writer.Write(MemoryMarshal.AsBytes(items.AsSpan()));

        private static T[] ReadAll<T>(BinaryReader reader, int count) where T : unmanaged
        {
            var items = new List<T>(Capacity(count));
            var chunk = new T[Capacity(count)];
            while (items.Count < count)
            {
                var length = Math.Min(count - items.Count, chunk.Length);
                var bytes = MemoryMarshal.AsBytes(chunk.AsSpan(0, length));
                while (bytes.Length > 0)
                {
                    var read = reader.Read(bytes);
                    if (read == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    bytes = bytes.Slice(read);
                }

                items.AddRange(new ArraySegment<T>(chunk, 0, length));
            }

            return items.ToArray();
        }

        private static int Capacity(int count) => Math.Min(count, PreallocateLimit);

        private static void WriteHeader(BinaryWriter writer, Replay replay)
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(replay.ScenePath ?? "");
            writer.Write(replay.Seed);
            writer.Write(replay.Weather ?? "");
            writer.Write(replay.SampleRate);
            writer.Write(replay.Duration);
            writer.Write((byte)replay.Result.Phase);
            writer.Write(replay.Result.WinningTeam);
            writer.Write(replay.Metadata ?? "");
            writer.Write(replay.Players.Count);
            foreach (var player in replay.Players)
            {
                writer.Write(player.Faction);
                writer.Write(player.Team);
                writer.Write(player.Name ?? "");
                writer.Write(player.Color.x);
                writer.Write(player.Color.y);
                writer.Write(player.Color.z);
                writer.Write(player.Color.w);
                writer.Write(player.Side);
                writer.Write((byte)player.Control);
                writer.Write((byte)player.Difficulty);
            }
        }

        private static Replay ReadHeader(BinaryReader reader)
        {
            if (reader.ReadInt32() != Magic)
            {
                throw new InvalidDataException("Not a HyperRTS replay.");
            }

            var version = reader.ReadInt32();
            if (version != Version)
            {
                throw new InvalidDataException($"replay from an older build (version {version})");
            }

            var replay = new Replay
            {
                ScenePath = reader.ReadString(),
                Seed = reader.ReadUInt32(),
                Weather = reader.ReadString(),
                SampleRate = reader.ReadSingle(),
                Duration = reader.ReadSingle(),
                Result = new MatchState { Phase = (MatchPhase)reader.ReadByte(), WinningTeam = reader.ReadByte() },
            };
            replay.Metadata = reader.ReadString();

            var players = ReadCount(reader, "players");
            for (var i = 0; i < players; i++)
            {
                replay.Players.Add(new ReplayPlayerInfo
                {
                    Faction = reader.ReadByte(),
                    Team = reader.ReadByte(),
                    Name = reader.ReadString(),
                    Color = new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Side = reader.ReadByte(),
                    Control = (PlayerControl)reader.ReadByte(),
                    Difficulty = (AIDifficulty)reader.ReadByte(),
                });
            }

            return replay;
        }
    }
}
