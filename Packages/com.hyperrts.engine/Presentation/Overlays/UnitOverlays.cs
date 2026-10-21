using System;
using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Veterancy;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Ground markers under selected and hovered entities, in relation colours, and health bars over the ones the
    /// visibility setting picks. Both read the drawn pose, so they stay on moving models.
    /// </summary>
    public sealed class UnitOverlays : IDisposable
    {
        // Room one standing soldier takes round its own origin.
        private const float FigureRadius = 0.8f;

        private readonly LiveQuery _living = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Health>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>(),
            ComponentType.Exclude<Inside>()));

        // Enabled-only, so the loop never asks EntityManager per entity whether it is selected.
        private readonly LiveQuery _selected = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<Health>()));

        private readonly LiveQuery _hovered = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<HoveredEntity>()));

        private readonly GroundMarkers _markers;
        private readonly HealthBars _bars;
        private readonly SelectionFades _fades = new();

        public UnitOverlays(Mesh grid, Mesh billboard, Material markerMaterial, Material barMaterial)
        {
            _markers = new GroundMarkers(grid, markerMaterial);
            _bars = new HealthBars(billboard, barMaterial);
        }

        public void Gather(MatchView view, OverlayStyle style, HealthBarVisibility visibility, float now)
        {
            var entityManager = view.EntityManager;
            var query = _living.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var healths = query.ToComponentDataArray<Health>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            using var selection = SelectedSet(entityManager);
            _hovered.In(entityManager).TryGetSingleton(out HoveredEntity hovered);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var health = healths[i];
                var focus = new OverlayFocus(selection.Contains(entity), entity == hovered.Value,
                    health.Current < health.Max);
                if (focus.ShowsMarker)
                {
                    var color = style.RelationColor(view.RelationTo(factions[i].Value));
                    AddMarker(entityManager, entity, transforms[i], color, focus.Selected, style, now);
                }

                if (focus.ShowsBar(visibility))
                {
                    AddBar(entityManager, entity, transforms[i].Position, health, style);
                }
            }

            _fades.EndFrame();
        }

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            _markers.Draw(layer);
            _bars.Draw(style, uiScale, layer);
        }

        public void Dispose()
        {
            _markers.Dispose();
            _bars.Dispose();
        }

        private void AddMarker(EntityManager entityManager, Entity entity, in LocalToWorld world, Color color,
            bool selected, OverlayStyle style, float now)
        {
            var layer = entityManager.HasComponent<NavAgent>(entity)
                ? entityManager.GetComponentData<NavAgent>(entity).Layer
                : NavLayer.Ground;
            var pose = new MarkerPose(world, layer);
            var footprint = Footprint(entityManager, entity, style.markerMargin);
            if (footprint.Shape == MarkerShape.Brackets)
            {
                pose = pose.AxisAligned();
            }

            if (selected)
            {
                var settle = SelectionFades.ScaleAndAlpha(_fades.Elapsed(entity, now), style.selectSeconds,
                    style.selectStartScale);
                footprint = footprint.Scaled(settle.x);
                color.a *= settle.y;
            }
            else
            {
                color.a *= style.hoverOpacity;
            }

            _markers.Add(pose, footprint, color, selected, style);
        }

        private void AddBar(EntityManager entityManager, Entity entity, float3 position, in Health health,
            OverlayStyle style)
        {
            var radius = EntityRadius.Get(entityManager, entity);
            var rank = entityManager.HasComponent<Experience>(entity)
                ? entityManager.GetComponentData<Experience>(entity).Rank
                : 0;
            var members = 0;
            var alive = 0;
            if (entityManager.HasBuffer<SquadFigure>(entity))
            {
                members = entityManager.GetBuffer<SquadFigure>(entity, true).Length;
                alive = SquadFigures.VisibleCount(health, members);
            }

            var top = EntityExtent.Top(entityManager, entity, position, radius);
            var bar = new HealthBarData(health, members, alive, rank, radius, style);
            _bars.Add(new Vector3(position.x, top, position.z), bar, style);
        }

        private static MarkerFootprint Footprint(EntityManager entityManager, Entity entity, float margin)
        {
            if (entityManager.HasBuffer<SquadFigure>(entity))
            {
                return MarkerFootprint.Round(SquadSpread(entityManager, entity), margin);
            }

            if (entityManager.HasComponent<NavAgent>(entity))
            {
                return MarkerFootprint.Agent(entityManager.GetComponentData<NavAgent>(entity), margin);
            }

            return entityManager.HasComponent<NavObstacle>(entity)
                ? MarkerFootprint.Building(entityManager.GetComponentData<NavObstacle>(entity), margin)
                : MarkerFootprint.Round(EntityRadius.Default, margin);
        }

        // Figures stand round the squad's centre, often past its nav radius, and one circle should take them all in.
        private static float SquadSpread(EntityManager entityManager, Entity entity)
        {
            var spread = EntityRadius.Get(entityManager, entity);
            foreach (var figure in entityManager.GetBuffer<SquadFigure>(entity, true))
            {
                var offset = entityManager.GetComponentData<LocalTransform>(figure.Figure).Position;
                spread = math.max(spread, math.length(offset.xz) + FigureRadius);
            }

            return spread;
        }

        private NativeHashSet<Entity> SelectedSet(EntityManager entityManager)
        {
            using var selected = _selected.In(entityManager).ToEntityArray(Allocator.Temp);
            var set = new NativeHashSet<Entity>(selected.Length, Allocator.Temp);
            foreach (var entity in selected)
            {
                set.Add(entity);
            }

            return set;
        }
    }
}
