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

        public static Replay Read(Stream stream)
        {
            using var zip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var reader = new BinaryReader(zip);
            var replay = ReadHeader(reader);
            replay.Frames = new ReplayFrame[reader.ReadInt32()];
            int entities = 0, removed = 0;
            for (var i = 0; i < replay.Frames.Length; i++)
            {
                var frame = new ReplayFrame { Time = reader.ReadSingle(), Keyframe = reader.ReadBoolean() };
                frame.EntityStart = entities;
                frame.EntityCount = reader.ReadInt32();
                frame.RemovedStart = removed;
                frame.RemovedCount = reader.ReadInt32();
                entities += frame.EntityCount;
                removed += frame.RemovedCount;
                replay.Frames[i] = frame;
            }

            replay.Entities = new ReplayEntity[entities];
            for (var i = 0; i < entities; i++)
            {
                replay.Entities[i] = ReadEntity(reader);
            }

            replay.Removed = new int[removed];
            for (var i = 0; i < removed; i++)
            {
                replay.Removed[i] = reader.ReadInt32();
            }

            return replay;
        }

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

            var players = reader.ReadInt32();
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
