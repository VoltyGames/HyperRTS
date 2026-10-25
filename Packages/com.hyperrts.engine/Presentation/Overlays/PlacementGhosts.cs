using System;
using System.Collections.Generic;
using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// The building being placed drawn as its real model in a see-through ghost material, one visual-only copy per
    /// spot, tinted by whether the server would accept it.
    /// </summary>
    public sealed class PlacementGhosts : IDisposable
    {
        private readonly LiveQuery _placement = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlacementState>()));

        private readonly Material _material;
        private readonly List<Entity> _ghosts = new();
        private readonly List<(BuildingSpot Spot, bool Valid)> _spots = new();
        private World _world;
        private Entity _source;
        private Entity _ghostPrefab;
        private LocalTransform _sourceTransform;

        public PlacementGhosts(Material material)
        {
            _material = material;
        }

        /// <summary>Matches the ghosts to this frame's placement: one per spot, gone once placement ends.</summary>
        public void Update(EntityManager entityManager, OverlayStyle style)
        {
            if (entityManager.World != _world)
            {
                Forget();
                _world = entityManager.World;
            }

            var source = ReadSpots(entityManager);
            if (source == Entity.Null)
            {
                Clear(entityManager);
                return;
            }

            if (source != _source || !entityManager.Exists(_ghostPrefab))
            {
                Clear(entityManager);
                Build(entityManager, source);
            }

            Resize(entityManager);
            var valid = (Vector4)style.placementValid.linear;
            var invalid = (Vector4)style.placementInvalid.linear;
            for (var i = 0; i < _spots.Count; i++)
            {
                var (spot, isValid) = _spots[i];
                Move(entityManager, _ghosts[i], spot, isValid ? valid : invalid);
            }
        }

        /// <summary>Destroys the ghosts and their visual prefab.</summary>
        public void Clear(EntityManager entityManager)
        {
            foreach (var ghost in _ghosts)
            {
                DestroyIfAlive(entityManager, ghost);
            }

            DestroyIfAlive(entityManager, _ghostPrefab);
            Forget();
        }

        /// <summary>Clears what is left in the last world, if it is still alive.</summary>
        public void Dispose()
        {
            if (_world is { IsCreated: true })
            {
                Clear(_world.EntityManager);
            }
        }

        private void Forget()
        {
            _ghosts.Clear();
            _ghostPrefab = Entity.Null;
            _source = Entity.Null;
        }

        /// <summary>Fills this frame's spots and returns the prefab being placed.</summary>
        private Entity ReadSpots(EntityManager entityManager)
        {
            _spots.Clear();
            var query = _placement.In(entityManager);
            var placing = query.TryGetSingleton(out PlacementState state) && state.Active;
            if (!placing || state.Prefab == Entity.Null)
            {
                return Entity.Null;
            }

            if (entityManager.HasComponent<LinePlacement>(state.Prefab))
            {
                var segments = entityManager.GetBuffer<PlacementSegment>(query.GetSingletonEntity(), true);
                foreach (var segment in segments)
                {
                    _spots.Add((segment.Spot, segment.Valid));
                }

                return state.Prefab;
            }

            // Only the pose matters here: the outline in CommandOverlays reads the footprint.
            var spot = new BuildingSpot(state.Position, state.Yaw, float2.zero, default);
            _spots.Add((spot, state.Valid));
            return state.Prefab;
        }

        /// <summary>A visual-only copy of <paramref name="source"/> whose every drawn piece uses the ghost material.</summary>
        private void Build(EntityManager entityManager, Entity source)
        {
            _source = source;
            _sourceTransform = entityManager.GetComponentData<LocalTransform>(source);
            _ghostPrefab = VisualPrefab.Build(entityManager, source);
            var ghostArrays = new Dictionary<RenderMeshArray, RenderMeshArray>();
            using var members = VisualPrefab.Members(entityManager, _ghostPrefab);
            foreach (var member in members)
            {
                if (!entityManager.HasComponent<MaterialMeshInfo>(member))
                {
                    continue;
                }

                var array = entityManager.GetSharedComponent<RenderMeshArray>(member);
                if (!ghostArrays.TryGetValue(array, out var ghostArray))
                {
                    ghostArray = Ghosted(array);
                    ghostArrays.Add(array, ghostArray);
                }

                entityManager.SetSharedComponent(member, ghostArray);
                entityManager.AddComponent<URPMaterialPropertyBaseColor>(member);
            }
        }

        /// <summary>The same meshes and sub-mesh layout with the ghost material in every material slot.</summary>
        private RenderMeshArray Ghosted(RenderMeshArray array)
        {
            var materials = new UnityObjectRef<Material>[array.MaterialReferences.Length];
            Array.Fill(materials, _material);
            return new RenderMeshArray(materials, array.MeshReferences, array.MaterialMeshIndices);
        }

        private void Resize(EntityManager entityManager)
        {
            while (_ghosts.Count < _spots.Count)
            {
                _ghosts.Add(entityManager.Instantiate(_ghostPrefab));
            }

            while (_ghosts.Count > _spots.Count)
            {
                DestroyIfAlive(entityManager, _ghosts[^1]);
                _ghosts.RemoveAt(_ghosts.Count - 1);
            }
        }

        private void Move(EntityManager entityManager, Entity ghost, in BuildingSpot spot, float4 color)
        {
            var rotation = math.mul(spot.Rotation, _sourceTransform.Rotation);
            entityManager.SetComponentData(ghost, LocalTransform.FromPositionRotationScale(
                spot.Center, rotation, _sourceTransform.Scale));
            using var members = VisualPrefab.Members(entityManager, ghost);
            foreach (var member in members)
            {
                if (entityManager.HasComponent<URPMaterialPropertyBaseColor>(member))
                {
                    entityManager.SetComponentData(member, new URPMaterialPropertyBaseColor { Value = color });
                }
            }
        }

        // Ghosts vanish with the scene section their source streamed in with.
        private static void DestroyIfAlive(EntityManager entityManager, Entity entity)
        {
            if (entityManager.Exists(entity))
            {
                entityManager.DestroyEntity(entity);
            }
        }
    }
}
