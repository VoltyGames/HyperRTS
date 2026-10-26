using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// The placement footprint outline with red cells where the spot is blocked, and rally markers of selected
    /// producers. The building's own ghost is <see cref="PlacementGhosts"/>.
    /// </summary>
    public sealed class CommandOverlays
    {
        private const float RallyRadius = 0.6f;
        private const float RallyLineWidth = 0.12f;
        private const float OutlineWidth = 0.15f;
        private const float Lift = 0.06f;

        // A 30 by 30 cell footprint, far past any building, so a huge drag can't flood the batch.
        private const int MaxBlockedCells = 900;

        private readonly LiveQuery _placement = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlacementState>()));

        private readonly LiveQuery _gridQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<NavGrid>()));

        private readonly LiveQuery _rallies = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<RallyPoint>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>()));

        private readonly InstanceBatch _validOutline;
        private readonly InstanceBatch _invalidOutline;
        private readonly InstanceBatch _blockedCells;
        private readonly InstanceBatch _rallyRings;
        private readonly InstanceBatch _rallyLines;
        private int _blockedCount;

        public CommandOverlays(Mesh ring, Mesh flatQuad)
        {
            _validOutline = new InstanceBatch(flatQuad);
            _invalidOutline = new InstanceBatch(flatQuad);
            _blockedCells = new InstanceBatch(flatQuad);
            _rallyRings = new InstanceBatch(ring);
            _rallyLines = new InstanceBatch(flatQuad);
        }

        public void Gather(MatchView view)
        {
            var entityManager = view.EntityManager;
            _blockedCount = 0;
            var placement = _placement.In(entityManager);
            var placing = placement.TryGetSingleton(out PlacementState state) && state.Active;
            if (placing && TryReadGrid(entityManager, out var grid))
            {
                AddPlacement(entityManager, placement.GetSingletonEntity(), state, grid);
            }

            var query = _rallies.In(entityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var rallies = query.ToComponentDataArray<RallyPoint>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < rallies.Length; i++)
            {
                if (factions[i].Value == view.Local.Faction)
                {
                    AddRally(transforms[i].Position, rallies[i].Position);
                }
            }
        }

        public void Draw(OverlayMaterials materials, OverlayStyle style, int layer)
        {
            // On top, or the very prop or wall that blocks a cell hides its marker.
            _blockedCells.Draw(materials.OnTop, style.placementInvalid, layer);
            _validOutline.Draw(materials.DepthTested, style.placementValid, layer);
            _invalidOutline.Draw(materials.DepthTested, style.placementInvalid, layer);
            _rallyRings.Draw(materials.DepthTested, style.rally, layer);
            _rallyLines.Draw(materials.DepthTested, style.rally, layer);
        }

        /// <summary>The cells are read on the main thread, so the jobs stamping them finish first.</summary>
        private bool TryReadGrid(EntityManager entityManager, out NavGrid grid)
        {
            var query = _gridQuery.In(entityManager);
            query.CompleteDependency();
            return query.TryGetSingleton(out grid);
        }

        private void AddPlacement(EntityManager entityManager, Entity singleton, in PlacementState state,
            in NavGrid grid)
        {
            if (entityManager.HasComponent<LinePlacement>(state.Prefab))
            {
                foreach (var segment in entityManager.GetBuffer<PlacementSegment>(singleton, true))
                {
                    AddSpot(segment.Spot, segment.Valid, grid);
                }

                return;
            }

            var footprint = entityManager.GetComponentData<NavObstacle>(state.Prefab).Size;
            var surface = BuildingPlacement.GetSurface(entityManager, state.Prefab);
            AddSpot(new BuildingSpot(state.Position, state.Yaw, footprint, surface), state.Valid, grid);
        }

        private void AddSpot(in BuildingSpot spot, bool valid, in NavGrid grid)
        {
            AddOutline(spot, valid ? _validOutline : _invalidOutline);
            if (!valid)
            {
                AddBlockedCells(spot, grid);
            }
        }

        /// <summary>Four flat strips along the turned box's sides, so the outline is exactly what the server checks.</summary>
        private static void AddOutline(in BuildingSpot spot, InstanceBatch batch)
        {
            var box = spot.Box;
            var rotation = (Quaternion)spot.Rotation;
            var height = spot.Center.y + Lift;
            var right = new Vector3(box.Right.x, 0f, box.Right.y) * box.Half.x;
            var forward = new Vector3(box.Forward.x, 0f, box.Forward.y) * box.Half.y;
            var center = new Vector3(box.Center.x, height, box.Center.y);
            var across = new Vector3(box.Half.x * 2f + OutlineWidth, 1f, OutlineWidth);
            var along = new Vector3(OutlineWidth, 1f, box.Half.y * 2f);
            batch.Add(Matrix4x4.TRS(center + forward, rotation, across));
            batch.Add(Matrix4x4.TRS(center - forward, rotation, across));
            batch.Add(Matrix4x4.TRS(center + right, rotation, along));
            batch.Add(Matrix4x4.TRS(center - right, rotation, along));
        }

        private void AddBlockedCells(in BuildingSpot spot, in NavGrid grid)
        {
            var box = spot.Box;
            var size = new Vector3(grid.CellSize, 1f, grid.CellSize);
            grid.GetBoxArea(box, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x && _blockedCount < MaxBlockedCells; x++)
                {
                    var cell = new int2(x, y);
                    if (!grid.InBox(box, cell) || !spot.Forbids(grid.Surface(cell)))
                    {
                        continue;
                    }

                    var center = grid.CellCenter(cell);
                    center.y = grid.SurfaceHeight(center) + Lift;
                    _blockedCells.Add(Matrix4x4.TRS(center, Quaternion.identity, size));
                    _blockedCount++;
                }
            }
        }

        private void AddRally(float3 from, float3 to)
        {
            var target = new Vector3(to.x, to.y + Lift, to.z);
            _rallyRings.Add(Matrix4x4.TRS(target, Quaternion.identity, new Vector3(RallyRadius, 1f, RallyRadius)));

            var start = new Vector3(from.x, to.y + Lift, from.z);
            var delta = target - start;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            var rotation = Quaternion.LookRotation(delta, Vector3.up);
            _rallyLines.Add(Matrix4x4.TRS((start + target) * 0.5f, rotation, new Vector3(RallyLineWidth, 1f, delta.magnitude)));
        }
    }
}
