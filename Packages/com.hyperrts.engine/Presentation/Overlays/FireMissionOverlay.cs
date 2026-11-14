using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// While a fire mission waits for its click, rings round each selected launcher from its minimum to its maximum
    /// range and, at the pointer, the circle one salvo covers: filled when a launcher reaches it from where it
    /// stands, an outline when every launcher would first drive into range.
    /// </summary>
    public sealed class FireMissionOverlay : IDisposable
    {
        private readonly LiveQuery _launchers = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<Owner>(),
            ComponentType.ReadOnly<WeaponMount>(),
            ComponentType.ReadOnly<LocalToWorld>()));

        private readonly LiveSingleton<HoveredEntity> _hovered = new();
        private readonly GroundMarkers _markers;

        public FireMissionOverlay(Mesh grid, Material markerMaterial)
        {
            _markers = new GroundMarkers(grid, markerMaterial);
        }

        public void Gather(MatchView view, OverlayStyle style)
        {
            if (view.Pending.Type != CommandType.FireMission)
            {
                return;
            }

            var entityManager = view.EntityManager;
            _hovered.TryGet(entityManager, out var hovered);
            var query = _launchers.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
            using var worlds = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var blast = 0f;
            var reached = false;
            for (var i = 0; i < entities.Length; i++)
            {
                var mounts = entityManager.GetBuffer<WeaponMount>(entities[i], true);
                if (owners[i].Value != view.Local.Owner || !FireMissionReach.TryRead(mounts, out var reach))
                {
                    continue;
                }

                AddRings(new MarkerPose(worlds[i], NavLayer.Ground), reach, style);
                blast = math.max(blast, reach.Blast);
                reached |= hovered.HasGround && reach.Covers(worlds[i].Position, hovered.Ground);
            }

            if (hovered.HasGround && blast > 0f)
            {
                AddBlast(hovered.Ground, blast, reached, style);
            }
        }

        public void Draw(int layer) => _markers.Draw(layer);

        public void Dispose() => _markers.Dispose();

        private void AddRings(in MarkerPose pose, in FireMissionReach reach, OverlayStyle style)
        {
            var outline = new MarkerLook(style.markerLine, style.markerFade, 0f);
            _markers.Add(pose, MarkerFootprint.Round(reach.Band.Reach, 0f), style.fireReach, outline);
            if (reach.Band.Floor <= 0f)
            {
                return;
            }

            // Fading over the whole radius makes the dead zone a soft disc rather than a rim.
            var dead = new MarkerLook(style.markerLine, reach.Band.Floor, style.fireFill);
            _markers.Add(pose, MarkerFootprint.Round(reach.Band.Floor, 0f), style.fireDeadZone, dead);
        }

        private void AddBlast(float3 point, float radius, bool reached, OverlayStyle style)
        {
            var footprint = MarkerFootprint.Round(radius, 0f);
            var pose = new MarkerPose(point);
            if (reached)
            {
                _markers.Add(pose, footprint, style.fireReady, new MarkerLook(style.markerLine, radius, style.fireFill));
                return;
            }

            _markers.Add(pose, footprint, style.fireMove, new MarkerLook(style.markerLine, style.markerFade, 0f));
        }
    }
}
