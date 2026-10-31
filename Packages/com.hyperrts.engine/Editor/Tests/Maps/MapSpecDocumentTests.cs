using System.Collections;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Json;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>
    /// The Map Editor's document: an edit saves at once and diffs like a hand edit, undo restores the file, and a file
    /// written from outside reloads, also while the window is open.
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
            File.WriteAllText(SpecPath, MapJsonFormat.Format(Original));
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
        public void AnEditSavesOnlyWhatChanged()
        {
            _document.Edit("Move", spec => spec.roads[0].points[1] = new MapPoint(120, 10));

            Assert.AreEqual(Formatted(Original.Replace("{\"x\": 100, \"z\": 0}", "{\"x\": 120, \"z\": 10}")), Saved());
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
        public void UndoRestoresTheFile()
        {
            var road = new MapFeatureRef(MapFeatureKinds.Find(nameof(MapSpec.roads)), 0);
            Undo.IncrementCurrentGroup();
            MapFeatureEdits.SetPoints(_document, road, new[] { new Vector2(-100, 0), new Vector2(50, 50) }, true);
            // Our own save comes back as an import; it must not reload and drop the undo history.
            _document.Reload();
            Assert.AreNotEqual(Formatted(Original), Saved());

            Undo.PerformUndo();

            Assert.AreEqual(Formatted(Original), Saved());
            Assert.AreEqual(100f, _document.Spec.roads[0].points[1].x);
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

            File.WriteAllText(SpecPath, MapJsonFormat.Format(Original.Replace("Dirt", "Paved")));
            _document.Reload();

            Assert.AreEqual("Paved", _document.Spec.roads[0].kind);
            Assert.AreEqual(1, changes);
        }

        [UnityTest]
        public IEnumerator AnOutsideWriteReloadsTheOpenWindow()
        {
            MapEditorWindow.Open(SpecPath);
            var window = EditorWindow.GetWindow<MapEditorWindow>();
            var shown = Shown();
            Assert.IsNotNull(shown, "The window holds no document for the spec.");

            File.WriteAllText(SpecPath, MapJsonFormat.Format(Original.Replace("Dirt", "Paved")));
            AssetDatabase.ImportAsset(SpecPath);
            for (var frame = 0; frame < 10 && shown.Spec.roads[0].kind != "Paved"; frame++)
            {
                yield return null;
            }

            Assert.AreEqual("Paved", shown.Spec.roads[0].kind);
            window.Close();
            yield return null;
            Assert.IsTrue(shown == null, "Closing the window leaves its document behind.");
        }

        private MapSpecDocument Shown() => Resources.FindObjectsOfTypeAll<MapSpecDocument>()
            .FirstOrDefault(document => document != _document && document.Path == SpecPath);

        private static string Saved() => File.ReadAllText(SpecPath);

        private static string Formatted(string json) => MapJsonFormat.Format(json);
    }
}
