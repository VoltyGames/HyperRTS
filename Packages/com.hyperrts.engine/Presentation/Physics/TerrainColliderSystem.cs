using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>
    /// Keeps the active map terrain in the presented physics world as static heightfield tiles, so bodies land on the
    /// ground the player sees. A swapped terrain rebuilds them all; heights read back from the GPU, as after craters,
    /// rebuild only the tiles they touch.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TerrainColliderSystem : SystemBase
    {
        /// <summary>Heightmap quads along a tile's side; a crater rebuilds a tile, not the map.</summary>
        public const int TileQuads = 128;

        private static readonly ProfilerMarker RebuildMarker = new("TerrainColliderSystem.Rebuild");

        private Terrain _terrain;
        private TerrainData _data;
        private int _tilesAcross;
        private Entity[] _tiles = System.Array.Empty<Entity>();
        private BlobAssetReference<Unity.Physics.Collider>[] _colliders = System.Array.Empty<BlobAssetReference<Unity.Physics.Collider>>();
        private bool[] _dirty = System.Array.Empty<bool>();
        private bool _anyDirty;
        private EntityArchetype _tileArchetype;

        protected override void OnCreate()
        {
            TerrainCallbacks.heightmapChanged += OnHeightsChanged;
            _tileArchetype = EntityManager.CreateArchetype(typeof(PhysicsCollider), typeof(LocalTransform),
                typeof(LocalToWorld), typeof(PhysicsWorldIndex));
        }

        protected override void OnDestroy()
        {
            TerrainCallbacks.heightmapChanged -= OnHeightsChanged;
            Release();
        }

        protected override void OnUpdate()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Release();
                return;
            }

            var swapped = terrain != _terrain || terrain.terrainData != _data;
            if (swapped)
            {
                Release();
                Adopt(terrain);
            }

            if (!_anyDirty)
            {
                return;
            }

            using (RebuildMarker.Auto())
            {
                RebuildDirtyTiles();
            }

            _anyDirty = false;
        }

        private void Adopt(Terrain terrain)
        {
            _terrain = terrain;
            _data = terrain.terrainData;
            _tilesAcross = (_data.heightmapResolution - 2) / TileQuads + 1;
            _tiles = new Entity[_tilesAcross * _tilesAcross];
            _colliders = new BlobAssetReference<Unity.Physics.Collider>[_tiles.Length];
            _dirty = new bool[_tiles.Length];
            System.Array.Fill(_dirty, true);
            _anyDirty = true;
        }

        private void RebuildDirtyTiles()
        {
            for (var i = 0; i < _dirty.Length; i++)
            {
                if (_dirty[i])
                {
                    _dirty[i] = false;
                    RebuildTile(i % _tilesAcross, i / _tilesAcross);
                }
            }
        }

        // Only heights already back in CPU memory: the GPU-only dent before a sync has nothing to read yet.
        private void OnHeightsChanged(Terrain terrain, RectInt region, bool synched)
        {
            if (!synched || terrain != _terrain)
            {
                return;
            }

            // A sample on a tile edge belongs to the tiles on both sides of it.
            var first = math.max(0, new int2(region.xMin - 1, region.yMin - 1) / TileQuads);
            var last = math.min(_tilesAcross - 1, new int2(region.xMax - 1, region.yMax - 1) / TileQuads);
            for (var z = first.y; z <= last.y; z++)
            {
                for (var x = first.x; x <= last.x; x++)
                {
                    _dirty[z * _tilesAcross + x] = true;
                }
            }

            _anyDirty = true;
        }

        private void RebuildTile(int x, int z)
        {
            var index = z * _tilesAcross + x;
            ReleaseTile(index);
            var start = new int2(x, z) * TileQuads;
            var count = math.min(TileQuads + 1, _data.heightmapResolution - start);
            var collider = CreateCollider(_data, start, count, out var spacing);
            var corner = (float3)_terrain.GetPosition() + new float3(start.x * spacing.x, 0f, start.y * spacing.y);
            var place = LocalTransform.FromPosition(corner);
            var tile = PhysicsGroup.CreateCollider(EntityManager, _tileArchetype, collider, place);
            EntityManager.SetComponentData(tile, new LocalToWorld { Value = place.ToMatrix() });
            _tiles[index] = tile;
            _colliders[index] = collider;
        }

        private static BlobAssetReference<Unity.Physics.Collider> CreateCollider(TerrainData data, int2 start, int2 count, out float2 spacing)
        {
            var rows = data.GetHeights(start.x, start.y, count.x, count.y);
            var heights = new NativeArray<float>(count.x * count.y, Allocator.Temp);
            for (var z = 0; z < count.y; z++)
            {
                for (var x = 0; x < count.x; x++)
                {
                    heights[z * count.x + x] = rows[z, x];
                }
            }

            var size = (float3)data.size;
            spacing = size.xz / (data.heightmapResolution - 1);
            var scale = new float3(spacing.x, size.y, spacing.y);
            // Triangles, not vertex samples: a capsule lying on a slope must not sink through its edges.
            return Unity.Physics.TerrainCollider.Create(heights, count, scale,
                Unity.Physics.TerrainCollider.CollisionMethod.Triangles, PhysicsGroup.GroundFilter);
        }

        private void ReleaseTile(int index)
        {
            if (EntityManager.Exists(_tiles[index]))
            {
                EntityManager.DestroyEntity(_tiles[index]);
            }

            if (_colliders[index].IsCreated)
            {
                _colliders[index].Dispose();
            }

            _tiles[index] = Entity.Null;
            _colliders[index] = default;
        }

        private void Release()
        {
            for (var i = 0; i < _tiles.Length; i++)
            {
                ReleaseTile(i);
            }

            _tiles = System.Array.Empty<Entity>();
            _colliders = System.Array.Empty<BlobAssetReference<Unity.Physics.Collider>>();
            _terrain = null;
            _data = null;
            _dirty = System.Array.Empty<bool>();
            _anyDirty = false;
        }
    }
}
