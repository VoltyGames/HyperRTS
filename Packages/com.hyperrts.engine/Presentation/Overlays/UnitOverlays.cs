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
            var handles = new UnitHandles(entityManager);
            _hovered.In(entityManager).TryGetSingleton(out HoveredEntity hovered);
            using var chunks = _living.In(entityManager).ToArchetypeChunkArray(Allocator.Temp);
            foreach (var chunk in chunks)
            {
                var entities = chunk.GetNativeArray(handles.Entities);
                var factions = chunk.GetNativeArray(ref handles.Factions);
                var healths = chunk.GetNativeArray(ref handles.Healths);
                var worlds = chunk.GetNativeArray(ref handles.Worlds);
                for (var i = 0; i < chunk.Count; i++)
                {
                    if (handles.IsInside(chunk, i))
                    {
                        continue;
                    }

                    var health = healths[i];
                    var focus = new OverlayFocus(handles.IsSelected(chunk, i), entities[i] == hovered.Value,
                        health.Current < health.Max);
                    var showsBar = focus.ShowsBar(visibility);
                    if (!focus.ShowsMarker && !showsBar)
                    {
                        continue;
                    }

                    var unit = handles.Unit(chunk, i);
                    if (focus.ShowsMarker)
                    {
                        var color = style.RelationColor(view.RelationTo(factions[i].Value));
                        AddMarker(entityManager, entities[i], unit, worlds[i], color, focus.Selected, style, now);
                    }

                    if (showsBar)
                    {
                        AddBar(entityManager, entities[i], unit, worlds[i].Position, health, style);
                    }
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

        private void AddMarker(EntityManager entityManager, Entity entity, in OverlayUnit unit, in LocalToWorld world,
            Color color, bool selected, OverlayStyle style, float now)
        {
            var pose = new MarkerPose(world, unit.Layer);
            var footprint = unit.IsSquad
                ? MarkerFootprint.Round(SquadSpread(entityManager, unit), style.markerMargin)
                : unit.Footprint(style.markerMargin);
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

        private void AddBar(EntityManager entityManager, Entity entity, in OverlayUnit unit, float3 position,
            in Health health, OverlayStyle style)
        {
            var alive = unit.IsSquad ? SquadFigures.VisibleCount(health, unit.Members) : 0;
            var top = EntityExtent.Top(entityManager, entity, position, unit.Radius);
            var bar = new HealthBarData(health, unit.Members, alive, unit.Rank, unit.Radius, style);
            _bars.Add(new Vector3(position.x, top, position.z), bar, style);
        }

        // Figures stand round the squad's centre, often past its nav radius, and one circle should take them all in.
        private static float SquadSpread(EntityManager entityManager, in OverlayUnit unit)
        {
            var spread = unit.Radius;
            foreach (var figure in unit.Figures)
            {
                var offset = entityManager.GetComponentData<LocalTransform>(figure.Figure).Position;
                spread = math.max(spread, math.length(offset.xz) + FigureRadius);
            }

            return spread;
        }

        // Chunk reads skip the job sync EntityManager.GetComponentData does, so the constructor completes writers first.
        private struct UnitHandles
        {
            public readonly EntityTypeHandle Entities;
            public ComponentTypeHandle<Faction> Factions;
            public ComponentTypeHandle<Health> Healths;
            public ComponentTypeHandle<LocalToWorld> Worlds;
            private ComponentTypeHandle<Selected> _selected;
            private ComponentTypeHandle<Inside> _inside;
            private ComponentTypeHandle<NavAgent> _agents;
            private ComponentTypeHandle<NavObstacle> _obstacles;
            private ComponentTypeHandle<Experience> _experience;
            private BufferTypeHandle<SquadFigure> _figures;

            public UnitHandles(EntityManager entityManager)
            {
                Entities = entityManager.GetEntityTypeHandle();
                Factions = Read<Faction>(entityManager);
                Healths = Read<Health>(entityManager);
                Worlds = Read<LocalToWorld>(entityManager);
                _selected = Read<Selected>(entityManager);
                _inside = Read<Inside>(entityManager);
                _agents = Read<NavAgent>(entityManager);
                _obstacles = Read<NavObstacle>(entityManager);
                _experience = Read<Experience>(entityManager);
                entityManager.CompleteDependencyBeforeRO<SquadFigure>();
                _figures = entityManager.GetBufferTypeHandle<SquadFigure>(true);
            }

            public bool IsSelected(in ArchetypeChunk chunk, int index) =>
                chunk.Has(ref _selected) && chunk.IsComponentEnabled(ref _selected, index);

            // The query's Exclude<Inside> keeps whole chunks; an enabled bit has to be checked per entity.
            public bool IsInside(in ArchetypeChunk chunk, int index) =>
                chunk.Has(ref _inside) && chunk.IsComponentEnabled(ref _inside, index);

            public OverlayUnit Unit(in ArchetypeChunk chunk, int index) => new(
                chunk.Has(ref _agents) ? chunk.GetNativeArray(ref _agents)[index] : null,
                chunk.Has(ref _obstacles) ? chunk.GetNativeArray(ref _obstacles)[index] : null,
                chunk.Has(ref _experience) ? chunk.GetNativeArray(ref _experience)[index].Rank : 0,
                chunk.Has(ref _figures) ? chunk.GetBufferAccessor(ref _figures)[index] : default);

            private static ComponentTypeHandle<T> Read<T>(EntityManager entityManager) where T : unmanaged, IComponentData
            {
                entityManager.CompleteDependencyBeforeRO<T>();
                return entityManager.GetComponentTypeHandle<T>(true);
            }
        }
    }
}
