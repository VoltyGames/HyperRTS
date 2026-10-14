using System.IO;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Replay headers carry game metadata and sides, and load on their own.</summary>
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
            Metadata = "{\"map\":\"desert\"}",
            Result = new MatchState { Phase = MatchPhase.Ended, WinningTeam = 2 },
            Players =
            {
                new ReplayPlayerInfo { Faction = 1, Team = 1, Name = "Ivanov", Color = new float4(1f), Side = 3 },
                new ReplayPlayerInfo { Faction = 3, Team = 2, Name = "AI", Color = new float4(0.5f), Side = 1 },
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
            Assert.AreEqual(2, header.Result.WinningTeam);
            Assert.IsEmpty(header.Frames, "samples are not read for a header");
            Assert.AreEqual(1, ReplaySerializer.Load(_path).Frames.Length);
        }

        [Test]
        public void Setup_RebuildsRecordedPlayers_AndClosesTheRest()
        {
            var setup = ReplaySetup.ToMatchSetup(Sample());

            Assert.AreEqual(3, setup.Slots.Length);
            Assert.AreEqual(PlayerControl.LocalHuman, setup.Slots[0].Control);
            Assert.AreEqual("Ivanov", setup.Slots[0].Name.ToString());
            Assert.IsFalse(setup.Slots[1].Open, "faction 2 wasn't in the match");
            Assert.AreEqual(1, setup.Slots[2].Side);
            Assert.AreEqual(2, setup.Slots[2].Team);
        }
    }
}
