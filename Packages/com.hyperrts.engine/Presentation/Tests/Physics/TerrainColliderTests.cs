using System.Collections.Generic;
using HyperRTS.Presentation.Physics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Physics
{
    /// <summary>A dent in the terrain rebuilds only the heightfield tile it touches, which then holds the new heights.</summary>
    public class TerrainColliderTests
    {
        // 513 samples a side at 1 m: four tiles of 128 quads across.
        private const int Resolution = 513;
        private const float Height = 20f;

        private World _world;
        private TerrainColliderSystem _system;
        private TerrainData _data;
        private Terrain _terrain;
        private readonly List<Terrain> _hidden = new();

        [SetUp]
        public void SetUp()
        {
            // An open map's terrain would be the active one instead.
            foreach (var other in Terrain.activeTerrains)
            {
                other.enabled = false;
                _hidden.Add(other);
            }

            _data = new TerrainData { heightmapResolution = Resolution, size = new Vector3(512f, Height, 512f) };
            _terrain = Terrain.CreateTerrainGameObject(_data).GetComponent<Terrain>();
            _world = new World("Terrain Collider Tests");
            _system = _world.GetOrCreateSystemManaged<TerrainColliderSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_terrain.gameObject);
            Object.DestroyImmediate(_data);
            foreach (var other in _hidden)
            {
                other.enabled = true;
            }

            _hidden.Clear();
        }

        [Test]
        public void Dent_RebuildsOnlyTheTileItTouches_WithTheNewHeights()
        {
            _system.Update();
            var before = Tiles();
            Assert.AreEqual(16, before.Count, "four tiles a side");

            // Five metres high on samples 200 to 204, inside tile (1, 1), which spans samples 128 to 256.
            var heights = new float[5, 5];
            for (var z = 0; z < 5; z++)
            {
                for (var x = 0; x < 5; x++)
                {
                    heights[z, x] = 5f / Height;
                }
            }

            _data.SetHeights(200, 200, heights);
            _system.Update();
            var after = Tiles();

            var dented = new int2(128, 128);
            foreach (var (corner, tile) in after)
            {
                var rebuilt = before[corner] != tile;
                Assert.AreEqual(corner.Equals(dented), rebuilt, $"tile at {corner} rebuilt");
            }

            var collider = _world.EntityManager.GetComponentData<PhysicsCollider>(after[dented]).Value;
            var ray = new RaycastInput
            {
                Start = new float3(74f, 30f, 74f), End = new float3(74f, -10f, 74f), Filter = CollisionFilter.Default,
            };
            Assert.IsTrue(collider.Value.CastRay(ray, out var hit), "the ray finds the tile");
            Assert.AreEqual(5f, hit.Position.y, 0.05f, "the dented height");
        }

        // Each tile by its corner on the map, in whole metres.
        private Dictionary<int2, Entity> Tiles()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(PhysicsCollider), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            var tiles = new Dictionary<int2, Entity>();
            foreach (var entity in entities)
            {
                var corner = _world.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                tiles.Add((int2)math.round(corner.xz), entity);
            }

            return tiles;
        }
    }
}
