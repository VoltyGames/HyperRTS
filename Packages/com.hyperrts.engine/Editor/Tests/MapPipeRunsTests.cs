using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Planning.Settlements;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Street pipe runs: no run shorter than the kit's shortest, and an arch over each short gap inside one.</summary>
    public class MapPipeRunsTests
    {
        private static bool[] Spots(string pattern) => pattern.Select(c => c == '#').ToArray();

        private static string Pattern(bool[] kept) => new(kept.Select(k => k ? '#' : '.').ToArray());

        [Test]
        public void RunsShorterThanTheShortestRun_AreLeftOut()
        {
            var arches = new List<(int First, int Count)>();

            var kept = MapPipeRuns.Keep(Spots("##....#....####"), 3, 1, arches);

            Assert.AreEqual("...........####", Pattern(kept));
            Assert.IsEmpty(arches);
        }

        [Test]
        public void AShortGapOverARoad_IsBridged_AndJoinsTheRunsEitherSide()
        {
            var arches = new List<(int First, int Count)>();

            var kept = MapPipeRuns.Keep(Spots("##..##......#"), 3, 2, arches);

            Assert.AreEqual("##..##.......", Pattern(kept), "two runs of 2 across the gap make 4; the lone section goes");
            Assert.AreEqual(new List<(int, int)> { (2, 2) }, arches);
        }

        [Test]
        public void AGapWiderThanAnArchSpans_SplitsTheRuns()
        {
            var arches = new List<(int First, int Count)>();

            var kept = MapPipeRuns.Keep(Spots("###...###"), 3, 2, arches);

            Assert.AreEqual("###...###", Pattern(kept));
            Assert.IsEmpty(arches);
        }
    }
}
