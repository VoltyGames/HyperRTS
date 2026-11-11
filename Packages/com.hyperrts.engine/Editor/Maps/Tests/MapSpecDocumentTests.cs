using System.IO;
using HyperRTS.Editor.Common.Json;
using HyperRTS.Editor.Maps.Designer.Canvas;
using HyperRTS.Editor.Maps.Designer.Features;
using HyperRTS.Editor.Maps.Designer.Tools;
using HyperRTS.Editor.Maps.Designer.Window;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests
{
    /// <summary>
    /// The Map Editor's document: an edit saves at once and diffs like a hand edit, and a file
    /// written from outside reloads.
    /// </summary>
    public class MapSpecDocumentTests
    {
        private const string Folder = "Assets/MapSpecDocumentTests";
        private const string SpecPath = Folder + "/Test.map.json";

        private const string Original = "{\"name\": \"Test\", \"symmetry\": \"rotate-180\", \"size\": {\"x\": 400, \"z\": 400}, " +
            "\"roads\": [{\"kind\": \"Dirt\", \"points\": [{\"x\": -100, \"z\": 0}, {\"x\": 100, \"z\": 0}]}]}";

        private MapSpecDocument _document;
        private bool _auto;

        [SetUp]
        public void WriteSpec()
        {
            _auto = MapLivePreview.Auto;
            MapLivePreview.Auto = false;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(SpecPath, JsonFormat.Format(Original));
            AssetDatabase.ImportAsset(SpecPath);
            _document = MapSpecDocument.Open(SpecPath);
        }

        [TearDown]
        public void DeleteSpec()
        {
            Object.DestroyImmediate(_document);
            AssetDatabase.DeleteAsset(Folder);
            MapLivePreview.Auto = _auto;
        }

        [Test]
        public void AnAddedFeatureLeavesOutItsDefaults()
        {
            var hill = MapFeatureEdits.Add(_document, MapFeatureKinds.Find(nameof(MapSpec.hills)), new[] { new Vector2(10, 20) });

            Assert.AreEqual(0, hill.Index);
            Assert.AreEqual(Formatted(Original.TrimEnd('}') + ", \"hills\": [{\"pos\": {\"x\": 10, \"z\": 20}}]}"), Saved());
            Assert.IsTrue(MapSpecJson.SameContent(_document.Spec, MapSpecJson.Read(Saved())));
        }

        [Test]
        public void DeletingAnEarlierFeatureWarnsThatLaterOnesChange()
        {
            var roads = MapFeatureKinds.Find(nameof(MapSpec.roads));
            MapFeatureEdits.Add(_document, roads, new[] { new Vector2(0, -50), new Vector2(0, 50) });

            Assert.IsTrue(MapFeatureEdits.Delete(_document, new MapFeatureRef(roads, 0)));
            Assert.IsFalse(MapFeatureEdits.Delete(_document, new MapFeatureRef(roads, 0)));
            Assert.IsEmpty(MapSpecJson.Read(Saved()).roads);
        }

        [Test]
        public void DraggingAPointSnapsItAndSavesWhenTheDragEnds()
        {
            var view = new MapCanvasView { Size = new Vector2(400, 400) };
            var selection = new MapFeatureSelection();
            var context = new MapToolContext(view, _ => { }) { Document = _document, Selection = selection };
            var tool = new MapSelectTool(context);
            selection.Set(new MapFeatureRef(MapFeatureKinds.Find(nameof(MapSpec.roads)), 0));

            tool.Down(view.ToLocal(new Vector2(100, 0)), EventModifiers.None, 1);
            tool.Move(view.ToLocal(new Vector2(130.2f, 19.9f)));
            Assert.AreEqual(Formatted(Original), Saved(), "A drag saved before it ended.");
            tool.Up();

            Assert.AreEqual(new MapPoint(130, 20), _document.Spec.roads[0].points[1]);
            Assert.AreEqual(Formatted(Original.Replace("{\"x\": 100, \"z\": 0}", "{\"x\": 130, \"z\": 20}")), Saved());
        }

        [Test]
        public void ThePointToolAddsTheKindPickedAtTheCursor()
        {
            var view = new MapCanvasView { Size = new Vector2(400, 400) };
            var selection = new MapFeatureSelection();
            var context = new MapToolContext(view, _ => { }) { Document = _document, Selection = selection };
            var toolbox = new MapToolbox();
            toolbox.SetMode(MapToolMode.Point);
            toolbox.SetKind(MapFeatureKinds.Find(nameof(MapSpec.spawns)));

            new MapPointTool(context, toolbox).Down(view.ToLocal(new Vector2(-40.3f, 60.1f)), EventModifiers.None, 1);

            Assert.AreEqual(new MapFeatureRef(MapFeatureKinds.Find(nameof(MapSpec.spawns)), 0), selection.Current);
            Assert.AreEqual(new MapPoint(-40.5f, 60), MapSpecJson.Read(Saved()).spawns[0].pos);
        }

        [Test]
        public void AnOutsideWriteReloads()
        {
            var changes = 0;
            _document.Changed += () => changes++;

            File.WriteAllText(SpecPath, JsonFormat.Format(Original.Replace("Dirt", "Paved")));
            _document.Reload();

            Assert.AreEqual("Paved", _document.Spec.roads[0].kind);
            Assert.AreEqual(1, changes);
        }

        private static string Saved() => File.ReadAllText(SpecPath);

        private static string Formatted(string json) => JsonFormat.Format(json);
    }
}
