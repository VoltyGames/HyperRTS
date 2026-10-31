using HyperRTS.Editor.Maps.Json;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>A saved spec changes only what was edited in the file's own JSON, as a hand edit would.</summary>
    public class MapJsonChangesTests
    {
        private const string File = "{\"name\": \"Changes\", \"woods\": [" +
            "{\"kind\": \"Oak\", \"points\": [{\"x\": 0, \"z\": 0}, {\"x\": 10, \"z\": 0}, {\"x\": 5, \"z\": 10}]}, " +
            "{\"kind\": \"Pine\", \"points\": [{\"x\": 20, \"z\": 0}, {\"x\": 30, \"z\": 0}, {\"x\": 25, \"z\": 10}]}, " +
            "{\"kind\": \"Birch\", \"points\": [{\"x\": 40, \"z\": 0}, {\"x\": 50, \"z\": 0}, {\"x\": 45, \"z\": 10}]}]}";

        [Test]
        public void AnUnchangedSpecKeepsTheFile() => AssertWritten(File, _ => { });

        [Test]
        public void AChangedValueLeavesTheRestOfTheFileAlone() =>
            AssertWritten(File.Replace("{\"x\": 25, \"z\": 10}]}", "{\"x\": 25, \"z\": 10}], \"shared\": true}"),
                spec => spec.woods[1].shared = true);

        [Test]
        public void ADeletedEntryIsSplicedOut() =>
            AssertWritten(File.Replace(
                "{\"kind\": \"Pine\", \"points\": [{\"x\": 20, \"z\": 0}, {\"x\": 30, \"z\": 0}, {\"x\": 25, \"z\": 10}]}, ",
                string.Empty), spec => spec.woods.RemoveAt(1));

        [Test]
        public void AnAddedEntryLeavesOutItsDefaults() =>
            AssertWritten(File.Replace("]}]}", "]}], \"hills\": [{\"pos\": {\"x\": 7, \"z\": 8}}]}"),
                spec => spec.hills.Add(new MapHillSpec { pos = new MapPoint(7, 8) }));

        [Test]
        public void AnEditInsideAMissingObjectWritesOnlyTheEditedField() =>
            AssertWritten(File.Replace("]}]}", "]}], \"terrain\": {\"height\": 30}}"), spec => spec.terrain.height = 30f);

        private static void AssertWritten(string expected, System.Action<MapSpec> edit)
        {
            var file = MapJsonFormat.Format(File);
            var spec = MapSpecJson.Read(file);
            edit(spec);

            Assert.AreEqual(MapJsonFormat.Format(expected), MapSpecJson.WriteOver(file, spec));
        }
    }
}
