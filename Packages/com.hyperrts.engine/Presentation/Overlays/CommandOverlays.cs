using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
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
    public sealed class CommandOverlays : IDisposable
    {
        private const float RallyRadius = 0.6f;
        private const float RallyLineWidth = 0.12f;
        private const float OutlineWidth = 0.15f;
        private const float Lift = 0.06f;

        // A 30 by 30 cell footprint, far past any building, so a huge drag can't flood the batch.
        private const int MaxBlockedCells = 900;

        private readonly LiveSingleton<NavGrid> _grid = new();

        private readonly LiveQuery _rallies = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<RallyPoint>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>()));

        // Footprint outlines and rally lines: flat strips on the ground, tested against depth.
        private readonly InstanceBuffer<OverlayInstance> _strips;
        private readonly InstanceBuffer<OverlayInstance> _blockedCells;
        private readonly InstanceBuffer<OverlayInstance> _rallyRings;
        private int _blockedCount;

        public CommandOverlays(Mesh ring, Mesh flatQuad)
        {
            _strips = new InstanceBuffer<OverlayInstance>(flatQuad);
            _blockedCells = new InstanceBuffer<OverlayInstance>(flatQuad);
            _rallyRings = new InstanceBuffer<OverlayInstance>(ring);
        }

        public void Gather(MatchView view, OverlayStyle style)
        {
            var entityManager = view.EntityManager;
            _blockedCount = 0;
            var state = view.Placement;
            var placing = state.Active;
            if (placing && TryReadGrid(entityManager, out var grid))
            {
                AddPlacement(entityManager, view.PlacementEntity, state, grid, style);
            }

            var query = _rallies.In(entityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var rallies = query.ToComponentDataArray<RallyPoint>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < rallies.Length; i++)
            {
                if (factions[i].Value == view.Local.Faction)
                {
                    AddRally(transforms[i].Position, rallies[i].Position, style.rally);
                }
            }
        }

        public void Draw(OverlayMaterials materials, int layer)
        {
            // On top, or the very prop or wall that blocks a cell hides its marker.
            _blockedCells.Draw(materials.OnTop, layer);
            _strips.Draw(materials.DepthTested, layer);
            _rallyRings.Draw(materials.DepthTested, layer);
        }

        public void Dispose()
        {
            _strips.Dispose();
            _blockedCells.Dispose();
            _rallyRings.Dispose();
        }

        /// <summary>The cells are read on the main thread, so the jobs stamping them finish first.</summary>
        private bool TryReadGrid(EntityManager entityManager, out NavGrid grid)
        {
            _grid.Complete(entityManager);
            return _grid.TryGet(entityManager, out grid);
        }

        private void AddPlacement(EntityManager entityManager, Entity singleton, in PlacementState state,
            in NavGrid grid, OverlayStyle style)
        {
            if (entityManager.HasComponent<LinePlacement>(state.Prefab))
            {
                foreach (var segment in entityManager.GetBuffer<PlacementSegment>(singleton, true))
                {
                    AddSpot(segment.Spot, segment.Valid, grid, style);
                }

                return;
            }

            var footprint = entityManager.GetComponentData<NavObstacle>(state.Prefab).Size;
            var surface = BuildingPlacement.GetSurface(entityManager, state.Prefab);
            AddSpot(new BuildingSpot(state.Position, state.Yaw, footprint, surface), state.Valid, grid, style);
        }

        private void AddSpot(in BuildingSpot spot, bool valid, in NavGrid grid, OverlayStyle style)
        {
            AddOutline(spot, valid ? style.placementValid : style.placementInvalid);
            if (!valid)
            {
                AddBlockedCells(spot, grid, style.placementInvalid);
            }
        }

        /// <summary>Four flat strips along the turned box's sides, so the outline is exactly what the server checks.</summary>
        private void AddOutline(in BuildingSpot spot, Color color)
        {
            var box = spot.Box;
            var rotation = (Quaternion)spot.Rotation;
            var height = spot.Center.y + Lift;
            var right = new Vector3(box.Right.x, 0f, box.Right.y) * box.Half.x;
            var forward = new Vector3(box.Forward.x, 0f, box.Forward.y) * box.Half.y;
            var center = new Vector3(box.Center.x, height, box.Center.y);
            var across = new Vector3(box.Half.x * 2f + OutlineWidth, 1f, OutlineWidth);
            var along = new Vector3(OutlineWidth, 1f, box.Half.y * 2f);
            _strips.Add(new OverlayInstance(Matrix4x4.TRS(center + forward, rotation, across), color));
            _strips.Add(new OverlayInstance(Matrix4x4.TRS(center - forward, rotation, across), color));
            _strips.Add(new OverlayInstance(Matrix4x4.TRS(center + right, rotation, along), color));
            _strips.Add(new OverlayInstance(Matrix4x4.TRS(center - right, rotation, along), color));
        }

        private void AddBlockedCells(in BuildingSpot spot, in NavGrid grid, Color color)
        {
            var box = spot.Box;
            var size = new Vector3(grid.Grid.CellSize, 1f, grid.Grid.CellSize);
            foreach (var cell in grid.CellsUnder(box))
            {
                if (_blockedCount >= MaxBlockedCells)
                {
                    return;
                }

                if (!grid.InBox(box, cell) || !spot.Forbids(grid.Surface(cell)))
                {
                    continue;
                }

                var center = grid.Grid.CenterPoint(cell);
                center.y = grid.SurfaceHeight(center) + Lift;
                _blockedCells.Add(new OverlayInstance(Matrix4x4.TRS(center, Quaternion.identity, size), color));
                _blockedCount++;
            }
        }

        private void AddRally(float3 from, float3 to, Color color)
        {
            var target = new Vector3(to.x, to.y + Lift, to.z);
            var ring = Matrix4x4.TRS(target, Quaternion.identity, new Vector3(RallyRadius, 1f, RallyRadius));
            _rallyRings.Add(new OverlayInstance(ring, color));

            var start = new Vector3(from.x, to.y + Lift, from.z);
            var delta = target - start;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            var rotation = Quaternion.LookRotation(delta, Vector3.up);
            var line = Matrix4x4.TRS((start + target) * 0.5f, rotation, new Vector3(RallyLineWidth, 1f, delta.magnitude));
            _strips.Add(new OverlayInstance(line, color));
        }
    }
}
