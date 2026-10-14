using System.IO;
using System.IO.Compression;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Replay headers carry a version, game metadata, sides and control, and load on their own.</summary>
    public class ReplayFormatTests
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
    }
}
