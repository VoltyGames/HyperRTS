using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Simulation.Navigation.Areas;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Heights
{
    /// <summary>The ground under a bridge: the river's banks between its abutments, embankments behind them.</summary>
    public class MapEmbankmentTests
    {
        private const float RiverZ = -60f;
        private const float DeckLength = 30f;
        private const float ClearSpan = 24f;

        private GameObject _bridge;

        [SetUp]
        public void SetUp()
        {
            _bridge = new GameObject("Bridge");
            var deck = _bridge.AddComponent<NavAreaAuthoring>();
            deck.kind = NavAreaKind.Deck;
            deck.size = new Vector2(7f, DeckLength);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_bridge);

        [Test]
        public void Bridge_KeepsTheBanksBetweenItsAbutments_AndRaisesGentleEmbankmentsBehindThem()
        {
            var (open, _) = Shape(false);
            var (bridged, shapes) = Shape(true);
            var crossing = shapes.Crossings.Find(c => c.IsBridge && c.Position.y < 0f);

            foreach (var along in new[] { -10f, -6f, 0f, 6f, 10f })
            {
                foreach (var aside in new[] { 0f, 6f })
                {
                    var p = crossing.Position + crossing.Across * along + new Vector2(aside, 0f);
                    Assert.AreEqual(open.Height(p), bridged.Height(p), 1e-3f, $"the river's own ground {along} m along, {aside} m aside");
                }
            }

            foreach (var sign in new[] { -1f, 1f })
            {
                var landing = crossing.Position + crossing.Across * (sign * (DeckLength * 0.5f + 1.5f));
                Assert.AreEqual(crossing.Height, bridged.Height(landing), 0.2f, "the embankment meets the deck at its end");

                // Down the embankment's side, from the roadway's middle out to the open ground.
                for (var x = 0f; x < 30f; x += 1f)
                {
                    var here = landing + new Vector2(x, 0f);
                    var step = Mathf.Abs(bridged.Height(here + Vector2.right) - bridged.Height(here));
                    Assert.Less(step, 0.35f, $"a gentle side slope {x} m out");
                }
            }
        }

        private (MapHeights, MapShapes) Shape(bool withBridge)
        {
            var spec = MapFixture.Spec();
            var river = new MapRiverSpec
            {
                points = new List<MapPoint> { new(-110f, RiverZ), new(110f, RiverZ) },
                width = 10f, valleyWidth = 90f, widthVariation = 0f,
            };
            if (withBridge)
            {
                river.crossings.Add(new MapCrossingSpec { along = 0.5f, kind = MapCrossingSpec.Bridge, prop = "Bridge" });
            }

            spec.rivers.Add(river);
            var layout = MapFixture.Layout(spec);
            var palette = MapFixture.Item<MapPalette>("Palette");
            palette.entries.Add(MapFixture.Item<MapProp>("Bridge", i =>
            {
                i.prefab = _bridge;
                i.size = new Vector2(9f, DeckLength);
                i.clearSpan = ClearSpan;
            }));
            var shapes = MapFixture.Shapes(layout, palette, MapFixture.Item<MapClimate>("Climate"));
            return (MapHeightShaper.Shape(layout, shapes), shapes);
        }
    }
}
