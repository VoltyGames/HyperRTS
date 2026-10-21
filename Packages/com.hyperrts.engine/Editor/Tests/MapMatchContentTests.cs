using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Scenes;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>A map build fills the Match's sides from the palette and each slot's spawn and default side.</summary>
    public class MapMatchContentTests
    {
        private GameObject _prefab;
        private MapPalette _palette;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Headquarters");
            _palette = new MapPalette();
            _palette.entries.Add(new MapProp { key = "HQ", prefab = _prefab });
            foreach (var id in new[] { 1, 3 })
            {
                var side = new MapPaletteSide { side = id };
                side.startKit.Add(new MapStartItem { key = "HQ", offset = new Vector2(1f, 2f), yaw = 45f });
                _palette.sides.Add(side);
            }
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_prefab);

        [Test]
        public void Sides_ResolveKitKeysToPrefabs()
        {
            var sides = MapMatchContent.Sides(_palette);

            CollectionAssert.AreEqual(new byte[] { 1, 3 }, sides.Select(s => s.side));
            var item = sides[1].startKit.Single();
            Assert.AreSame(_prefab, item.prefab);
            Assert.AreEqual(new Vector2(1f, 2f), item.offset);
            Assert.AreEqual(45f, item.yaw);
            Assert.IsNull(sides[1].buildOrder);
        }

        [Test]
        public void Players_StandOnTheGroundAtTheirSpawns_AndTakeTurnsAtTheSides()
        {
            var spawns = new List<MapSpawn>
            {
                new(new Vector2(-50f, 0f), 90f, 1, 1, 0),
                new(new Vector2(50f, 0f), 270f, 2, 2, 1),
                new(new Vector2(0f, 50f), 180f, 3, 1, 2),
            };

            var players = MapMatchContent.Players(spawns, _palette, point => point.x * 0.1f);

            CollectionAssert.AreEqual(new byte[] { 1, 3, 1 }, players.Select(p => p.side));
            Assert.AreEqual(new Vector3(50f, 5f, 0f), players[1].spawnPosition);
            Assert.AreEqual(270f, players[1].spawnYaw);
            Assert.AreEqual(2, players[1].team);
        }
    }
}
