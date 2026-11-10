using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>
    /// Keeps a static heightfield of the active map terrain in the presented physics world, rebuilt when the terrain or
    /// its data is swapped, so bodies land on the ground the player sees.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TerrainColliderSystem : SystemBase
    {
        private Terrain _terrain;
        private TerrainData _data;
        private Entity _ground;
        private BlobAssetReference<Unity.Physics.Collider> _collider;

        protected override void OnDestroy() => Release();

        protected override void OnUpdate()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Release();
                return;
            }

            var unchanged = terrain == _terrain && terrain.terrainData == _data;
            if (unchanged)
            {
                return;
            }

            Release();
            _terrain = terrain;
            _data = terrain.terrainData;
            _collider = CreateCollider(_data);
            _ground = EntityManager.CreateEntity(typeof(PhysicsCollider), typeof(LocalTransform), typeof(LocalToWorld));
            var place = LocalTransform.FromPosition(terrain.GetPosition());
            EntityManager.SetComponentData(_ground, new PhysicsCollider { Value = _collider });
            EntityManager.SetComponentData(_ground, place);
            EntityManager.SetComponentData(_ground, new LocalToWorld { Value = place.ToMatrix() });
            EntityManager.AddSharedComponent(_ground, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
        }

        private static BlobAssetReference<Unity.Physics.Collider> CreateCollider(TerrainData data)
        {
            var resolution = data.heightmapResolution;
            var rows = data.GetHeights(0, 0, resolution, resolution);
            var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    heights[z * resolution + x] = rows[z, x];
                }
            }

            var size = (float3)data.size;
            var scale = new float3(size.x / (resolution - 1), size.y, size.z / (resolution - 1));
            // Triangles, not vertex samples: a capsule lying on a slope must not sink through its edges.
            var collider = Unity.Physics.TerrainCollider.Create(heights, new int2(resolution), scale,
                Unity.Physics.TerrainCollider.CollisionMethod.Triangles, PhysicsGroup.TerrainFilter);
            heights.Dispose();
            return collider;
        }

        private void Release()
        {
            if (EntityManager.Exists(_ground))
            {
                EntityManager.DestroyEntity(_ground);
            }

            if (_collider.IsCreated)
            {
                _collider.Dispose();
            }

            _ground = Entity.Null;
            _collider = default;
            _terrain = null;
            _data = null;
        }
    }
}
