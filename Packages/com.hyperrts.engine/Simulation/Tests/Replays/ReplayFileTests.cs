using System;
using System.IO;
using System.IO.Compression;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Replays
{
    /// <summary>The replay file: a full round trip, headers that load alone, version and corruption checks.</summary>
    public class ReplayFileTests
    {
        private string _path;

        [SetUp]
        public void SetUp() => _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".hrreplay");

        [TearDown]
        public void TearDown() => File.Delete(_path);

        private static Replay Sample() => new()
        {
            ScenePath = "Assets/Maps/Map.unity",
            SampleRate = 10f,
            Weather = "Rain",
            Metadata = "{\"map\":\"desert\"}",
            Result = new MatchState { Phase = MatchPhase.Ended, WinningTeam = 2 },
            Players =
            {
                new ReplayPlayerInfo { Faction = 1, Team = 1, Name = "Ivanov", Color = new float4(1f), Side = 3 },
                new ReplayPlayerInfo
                {
                    Faction = 3, Team = 2, Name = "", Color = new float4(0.5f), Side = 1, Control = PlayerControl.AI,
                    Difficulty = AIDifficulty.Hard,
                },
            },
            Frames = new[] { new ReplayFrame { Time = 0f, Keyframe = true } },
        };

        [Test]
        public void Header_LoadsAlone_WithMetadataAndSides()
        {
            ReplaySerializer.Save(Sample(), _path);

            var header = ReplaySerializer.LoadHeader(_path);

            Assert.AreEqual("{\"map\":\"desert\"}", header.Metadata);
            Assert.AreEqual(3, header.Players[0].Side);
            Assert.AreEqual(PlayerControl.AI, header.Players[1].Control);
            Assert.AreEqual(AIDifficulty.Hard, header.Players[1].Difficulty);
            Assert.AreEqual(2, header.Result.WinningTeam);
            Assert.AreEqual("Rain", header.Weather);
            Assert.IsEmpty(header.Frames, "samples are not read for a header");
            Assert.AreEqual(1, ReplaySerializer.Load(_path).Frames.Length);
        }

        [Test]
        public void Setup_RebuildsRecordedPlayers_AndClosesTheRest()
        {
            var setup = ReplaySetup.ToMatchSetup(Sample());

            Assert.AreEqual(3, setup.Slots.Length);
            Assert.AreEqual("Rain", setup.Weather.ToString(), "playback shows the recorded weather");
            Assert.AreEqual(PlayerControl.LocalHuman, setup.Slots[0].Control);
            Assert.AreEqual("Ivanov", setup.Slots[0].Name.ToString());
            Assert.IsFalse(setup.Slots[1].Open, "faction 2 wasn't in the match");
            Assert.AreEqual(1, setup.Slots[2].Side);
            Assert.AreEqual(2, setup.Slots[2].Team);
            Assert.AreEqual(PlayerControl.AI, setup.Slots[2].Control, "bots stay bots, so each viewer names them");
            Assert.AreEqual(AIDifficulty.Hard, setup.Slots[2].Difficulty);
        }

        [Test]
        public void Read_RejectsAnotherVersion()
        {
            using var memory = new MemoryStream();
            using (var zip = new GZipStream(memory, CompressionLevel.Fastest, leaveOpen: true))
            using (var writer = new BinaryWriter(zip))
            {
                writer.Write(0x4C505248);
                writer.Write(ReplaySerializer.Version - 1);
            }

            memory.Position = 0;

            var error = Assert.Throws<InvalidDataException>(() => ReplaySerializer.Read(memory));
            StringAssert.Contains($"version {ReplaySerializer.Version - 1}", error.Message);
        }

        [Test]
        public void Serializer_RoundTrips()
        {
            var replay = ReplayTests.Record(out _);
            replay.ScenePath = "Assets/Maps/Test.unity";
            replay.Seed = 0xC0FFEEu;
            replay.Result = new MatchState { Phase = MatchPhase.Ended, WinningTeam = 2 };

            using var memory = new MemoryStream();
            ReplaySerializer.Write(replay, memory);
            memory.Position = 0;
            var read = ReplaySerializer.Read(memory);

            Assert.AreEqual(replay.ScenePath, read.ScenePath);
            Assert.AreEqual(0xC0FFEEu, read.Seed);
            Assert.AreEqual(0xC0FFEEu, ReplaySetup.ToMatchSetup(read).Seed, "playback rebuilds the setup with its seed");
            Assert.AreEqual(replay.SampleRate, read.SampleRate);
            Assert.AreEqual(replay.Duration, read.Duration);
            Assert.AreEqual(MatchPhase.Ended, read.Result.Phase);
            Assert.AreEqual(2, read.Result.WinningTeam);
            Assert.AreEqual(replay.Players.Count, read.Players.Count);
            Assert.AreEqual(replay.Players[0].Name, read.Players[0].Name);
            Assert.AreEqual(replay.Players[1].Color, read.Players[1].Color);
            CollectionAssert.AreEqual(replay.Frames, read.Frames);
            CollectionAssert.AreEqual(replay.Entities, read.Entities);
            CollectionAssert.AreEqual(replay.Removed, read.Removed);
        }

        [Test]
        public void Serializer_RejectsCorruptCounts_AndTruncatedFiles()
        {
            var replay = ReplayTests.Record(out _);
            using var memory = new MemoryStream();
            ReplaySerializer.Write(replay, memory);

            using var truncated = Recompress(memory.ToArray(), bytes => bytes[..^7]);
            Assert.Throws<InvalidDataException>(() => ReplaySerializer.Read(truncated));

            // The frame count follows the header; a negative one must not allocate or loop.
            using var corrupt = Recompress(memory.ToArray(), bytes =>
            {
                var at = HeaderLength(replay);
                BitConverter.GetBytes(-5).CopyTo(bytes, at);
                return bytes;
            });
            Assert.Throws<InvalidDataException>(() => ReplaySerializer.Read(corrupt));
        }

        private static MemoryStream Recompress(byte[] zipped, Func<byte[], byte[]> edit)
        {
            var bytes = edit(Unzip(zipped));
            var output = new MemoryStream();
            using (var zip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                zip.Write(bytes, 0, bytes.Length);
            }

            output.Position = 0;
            return output;
        }

        private static byte[] Unzip(byte[] zipped)
        {
            using var raw = new MemoryStream();
            using (var unzip = new GZipStream(new MemoryStream(zipped), CompressionMode.Decompress))
            {
                unzip.CopyTo(raw);
            }

            return raw.ToArray();
        }

        /// <summary>Where the frame count sits: an empty replay is its header plus a zero frame count.</summary>
        private static int HeaderLength(Replay replay)
        {
            var empty = new Replay
            {
                ScenePath = replay.ScenePath, SampleRate = replay.SampleRate, Duration = replay.Duration,
                Players = replay.Players, Result = replay.Result,
            };
            using var zipped = new MemoryStream();
            ReplaySerializer.Write(empty, zipped);
            return Unzip(zipped.ToArray()).Length - sizeof(int);
        }
    }
}
