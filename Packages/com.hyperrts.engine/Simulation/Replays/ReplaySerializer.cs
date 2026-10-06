using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using HyperRTS.Simulation.Match;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>Reads and writes <see cref="Replay"/> files: a GZip stream of a small header and the samples.</summary>
    public static class ReplaySerializer
    {
        public const int FormatVersion = 1;
        private const int Magic = 0x4C505248; // "HRPL"

        /// <summary>Most elements reserved before any are read; larger lists grow as data arrives.</summary>
        private const int PreallocateLimit = 4096;

        public static void Save(Replay replay, string path)
        {
            using var file = File.Create(path);
            Write(replay, file);
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
            }

            foreach (var entity in replay.Entities)
            {
                WriteEntity(writer, entity);
            }

            foreach (var key in replay.Removed)
            {
                writer.Write(key);
            }
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
            long entities = 0, removed = 0;
            for (var i = 0; i < frameCount; i++)
            {
                var frame = new ReplayFrame { Time = reader.ReadSingle(), Keyframe = reader.ReadBoolean() };
                frame.EntityStart = (int)entities;
                frame.EntityCount = ReadCount(reader, "entity samples");
                frame.RemovedStart = (int)removed;
                frame.RemovedCount = ReadCount(reader, "removals");
                entities = Total(entities + frame.EntityCount, "entity samples");
                removed = Total(removed + frame.RemovedCount, "removals");
                frames.Add(frame);
            }

            replay.Frames = frames.ToArray();
            replay.Entities = ReadAll(reader, (int)entities, ReadEntity);
            replay.Removed = ReadAll(reader, (int)removed, r => r.ReadInt32());
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

        private static T[] ReadAll<T>(BinaryReader reader, int count, Func<BinaryReader, T> read)
        {
            var items = new List<T>(Capacity(count));
            for (var i = 0; i < count; i++)
            {
                items.Add(read(reader));
            }

            return items.ToArray();
        }

        private static int Capacity(int count) => Math.Min(count, PreallocateLimit);

        private static void WriteHeader(BinaryWriter writer, Replay replay)
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(replay.ScenePath ?? "");
            writer.Write(replay.SampleRate);
            writer.Write(replay.Duration);
            writer.Write((byte)replay.Result.Phase);
            writer.Write(replay.Result.WinningTeam);
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
            }
        }

        private static Replay ReadHeader(BinaryReader reader)
        {
            if (reader.ReadInt32() != Magic)
            {
                throw new InvalidDataException("Not a HyperRTS replay.");
            }

            var version = reader.ReadInt32();
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported replay version {version} (expected {FormatVersion}).");
            }

            var replay = new Replay
            {
                ScenePath = reader.ReadString(),
                SampleRate = reader.ReadSingle(),
                Duration = reader.ReadSingle(),
                Result = new MatchState { Phase = (MatchPhase)reader.ReadByte(), WinningTeam = reader.ReadByte() },
            };

            var players = ReadCount(reader, "players");
            for (var i = 0; i < players; i++)
            {
                replay.Players.Add(new ReplayPlayerInfo
                {
                    Faction = reader.ReadByte(),
                    Team = reader.ReadByte(),
                    Name = reader.ReadString(),
                    Color = new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                });
            }

            return replay;
        }

        private static void WriteEntity(BinaryWriter writer, in ReplayEntity entity)
        {
            writer.Write(entity.Key);
            writer.Write(entity.TypeId);
            writer.Write(entity.X);
            writer.Write(entity.Y);
            writer.Write(entity.Z);
            writer.Write(entity.Yaw);
            writer.Write(entity.Faction);
            writer.Write(entity.Health);
            writer.Write(entity.Progress);
        }

        private static ReplayEntity ReadEntity(BinaryReader reader) => new()
        {
            Key = reader.ReadInt32(),
            TypeId = reader.ReadInt32(),
            X = reader.ReadInt16(),
            Y = reader.ReadInt16(),
            Z = reader.ReadInt16(),
            Yaw = reader.ReadUInt16(),
            Faction = reader.ReadByte(),
            Health = reader.ReadByte(),
            Progress = reader.ReadByte(),
        };
    }
}
