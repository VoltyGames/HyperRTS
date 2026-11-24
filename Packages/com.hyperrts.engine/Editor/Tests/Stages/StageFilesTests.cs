using System;
using System.IO;
using HyperRTS.Editor.Stages;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Stages
{
    /// <summary>A stage's state from its files' times and its log's exit line.</summary>
    public class StageFilesTests
    {
        private const string Folder = "Temp/StageFilesTests";
        private const string Input = Folder + "/input.txt";
        private const string Output = Folder + "/output.png";
        private const string Absent = Folder + "/absent.png";

        private static readonly DateTime Made = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private StageLogFile _log;

        [SetUp]
        public void WriteFiles()
        {
            Directory.CreateDirectory(Folder);
            Touch(Input, Made.AddMinutes(-5));
            Touch(Output, Made);
            _log = new StageLogFile(Folder + "/stage.log");
            File.Delete(_log.Path);
        }

        [TearDown]
        public void DeleteFiles() => Directory.Delete(Folder, true);

        [Test]
        public void OutputsNewerThanInputsAreDone() => Assert.AreEqual(StageState.Done, State(Output));

        [Test]
        public void AnInputNewerThanTheOldestOutputIsOutOfDate()
        {
            Touch(Input, Made.AddMinutes(1));
            Assert.AreEqual(StageState.OutOfDate, State(Output));
        }

        [Test]
        public void AnAbsentOutputOrNoneIsMissing()
        {
            Assert.AreEqual(StageState.Missing, State(Output, Absent));
            Assert.AreEqual(StageState.Missing, State());
        }

        [Test]
        public void ALogEndingInAnExitCodeOtherThanZeroFailsAheadOfFreshOutputs()
        {
            _log.Write("baking\nno Blender", 1);
            Assert.IsTrue(_log.Failed);
            Assert.AreEqual(StageState.Failed, State(Output));

            _log.Write("baking", 0);
            Assert.AreEqual(StageState.Done, State(Output));
        }

        [Test]
        public void ALogStillBeingWrittenHasNotFailed()
        {
            File.WriteAllText(_log.Path, "exit code 3 in a line the tool printed\nrendering frame 12\n");
            Assert.IsFalse(_log.Failed);
        }

        [Test]
        public void ALogNewerThanTheOutputsCountsAsTheirTimeOnlyWhenItMarksThem()
        {
            Touch(Input, Made.AddMinutes(1));
            _log.Write("rewrote its own input", 0);
            File.SetLastWriteTimeUtc(_log.Path, Made.AddMinutes(2));
            Assert.AreEqual(StageState.OutOfDate, State(Output));
            Assert.AreEqual(StageState.Done, new StageFiles(_log, new[] { Input }, new[] { Output }, true).State());
        }

        private StageState State(params string[] outputs) => new StageFiles(_log, new[] { Input }, outputs).State();

        private static void Touch(string path, DateTime time)
        {
            File.WriteAllText(path, "x");
            File.SetLastWriteTimeUtc(path, time);
        }
    }
}
