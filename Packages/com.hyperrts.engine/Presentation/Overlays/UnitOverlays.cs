using HyperRTS.Presentation.Overlays.Bars;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Presentation.Overlays.Pips;
using HyperRTS.Simulation.Common.Infantry;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
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
    /// Ground markers under selected and hovered entities, owner-coloured pips over small visible
    /// mobile units, and health bars over the ones the visibility setting picks. All read the drawn pose.
    /// </summary>
    public sealed class UnitOverlays : IDisposable
    {
        private readonly LiveQuery _living = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Health>(),
            ComponentType.ReadOnly<Owner>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>(),
            ComponentType.Exclude<Inside>()));

        private readonly LiveSingleton<HoveredEntity> _hovered = new();
        private readonly LiveSingleton<SquadRules> _squadRules = new();

        private readonly GroundMarkers _markers;
        private readonly UnitPips _pips;
        private readonly HealthBars _bars;
        private readonly SelectionFades _fades = new();

        public UnitOverlays(Mesh grid, Mesh billboard, Material markerMaterial, Material barMaterial,
            Material pipMaterial)
        {
            _markers = new GroundMarkers(grid, markerMaterial);
            _pips = new UnitPips(billboard, pipMaterial);
            _bars = new HealthBars(billboard, barMaterial);
        }

        public void Gather(MatchView view, OverlayStyle style, HealthBarVisibility visibility, in ReferencePixels pixels,
            float now)
        {
            var entityManager = view.EntityManager;
            var handles = new UnitHandles(entityManager);
            _hovered.TryGet(entityManager, out var hovered);
            _squadRules.TryGet(entityManager, out var squadRules);
            var pass = new GatherPass
            {
                View = view,
                Style = style,
                Visibility = visibility,
                Pixels = pixels,
                Now = now,
                Hovered = hovered.Value,
                Rules = squadRules,
            };
            using var chunks = _living.In(entityManager).ToArchetypeChunkArray(Allocator.Temp);
            foreach (var chunk in chunks)
            {
                GatherChunk(pass, ref handles, chunk);
            }

            _fades.EndFrame();
        }

        private void GatherChunk(in GatherPass pass, ref UnitHandles handles, in ArchetypeChunk chunk)
        {
            var entities = chunk.GetNativeArray(handles.Entities);
            var owners = chunk.GetNativeArray(ref handles.Owners);
            var healths = chunk.GetNativeArray(ref handles.Healths);
            var worlds = chunk.GetNativeArray(ref handles.Worlds);
            var units = new ChunkUnits(chunk, ref handles);
            for (var i = 0; i < chunk.Count; i++)
            {
                if (handles.IsInside(chunk, i))
                {
                    continue;
                }

                var health = healths[i];
                GatherUnit(pass, new SeenUnit
                {
                    Entity = entities[i],
                    Owner = owners[i].Value,
                    Health = health,
                    World = worlds[i],
                    Unit = units.Unit(i),
                    Focus = new OverlayFocus(handles.IsSelected(chunk, i), entities[i] == pass.Hovered,
                        health.Current < health.Max),
                });
            }
        }

        private void GatherUnit(in GatherPass pass, in SeenUnit seen)
        {
            var showsBar = seen.Focus.ShowsBar(pass.Visibility);
            var teamMarked = seen.TeamMarked;
            if (!seen.Focus.ShowsMarker && !showsBar && !teamMarked)
            {
                return;
            }

            var style = pass.Style;
            var position = seen.World.Position;
            var pose = new MarkerPose(seen.World, seen.Unit.Layer);
            var footprint = MarkerSize(seen.Unit, style);
            var top = EntityExtent.Top(pass.View.EntityManager, seen.Entity, position, seen.Unit.Radius);
            var anchor = new Vector3(position.x, top, position.z);
            if (teamMarked)
            {
                AddPip(pass, pose, anchor, pass.View.GetColor(seen.Owner));
            }

            if (seen.Focus.ShowsMarker)
            {
                var color = style.RelationColor(pass.View.RelationTo(seen.Owner));
                var selected = seen.Focus.Selected;
                var seconds = selected ? _fades.Elapsed(seen.Entity, pass.Now) : 0f;
                AddMarker(pose, footprint, color, selected, seconds, style);
            }

            if (showsBar)
            {
                AddBar(seen.Unit, anchor, seen.Health, pass.Rules, style);
            }
        }

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            _markers.Draw(layer);
            _pips.Draw(style, uiScale, layer);
            _bars.Draw(style, uiScale, layer);
        }

        public void Dispose()
        {
            _markers.Dispose();
            _pips.Dispose();
            _bars.Dispose();
        }

        private static MarkerFootprint MarkerSize(in OverlayUnit unit, OverlayStyle style)
        {
            return unit.IsSquad
                ? MarkerFootprint.Round(unit.Spread, style.markerMargin)
                : unit.Footprint(style.markerMargin);
        }

        private void AddPip(in GatherPass pass, in MarkerPose pose, Vector3 top, Color owner)
        {
            var foot = new Vector3(top.x, pose.Position.y, top.z);
            if (UnitPips.Shows(pass.Pixels.Projected(foot, top), pass.Style))
            {
                _pips.Add(top, owner);
            }
        }

        private void AddMarker(in MarkerPose pose, MarkerFootprint footprint, Color color, bool selected, float selectedFor,
            OverlayStyle style)
        {
            if (selected)
            {
                var settle = SelectionFades.ScaleAndAlpha(selectedFor, style.selectSeconds, style.selectStartScale);
                footprint = footprint.Scaled(settle.x);
                color.a *= settle.y;
            }
            else
            {
                color.a *= style.hoverOpacity;
            }

            _markers.Add(pose, footprint, color, selected, style);
        }

        private void AddBar(in OverlayUnit unit, Vector3 anchor, in Health health, in SquadRules squadRules,
            OverlayStyle style)
        {
            var bar = new HealthBarData(health, unit.Rank, unit.Radius, SquadBarState.Read(unit, squadRules), style);
            _bars.Add(anchor, bar, style);
        }

        private struct GatherPass
        {
            public MatchView View;
            public OverlayStyle Style;
            public HealthBarVisibility Visibility;
            public ReferencePixels Pixels;
            public float Now;
            public Entity Hovered;
            public SquadRules Rules;
        }

        private struct SeenUnit
        {
            public Entity Entity;
            public byte Owner;
            public Health Health;
            public LocalToWorld World;
            public OverlayUnit Unit;
            public OverlayFocus Focus;

            // Owned mobile units only: buildings and map props carry no nav agent, and neutral ones have no team.
            public bool TeamMarked => Unit.Agent.HasValue && Owner != 0;
        }
    }
}
