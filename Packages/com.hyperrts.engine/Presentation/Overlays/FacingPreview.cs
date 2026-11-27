using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays;

/// <summary>
/// While a move is dragged to aim its facing: an arrow along the drag and, turned to it, the footprint outline of each
/// selected unit's formation slot, laid out by the same <see cref="FormationSlots"/> the order uses.
/// </summary>
public sealed class FacingPreview(Mesh grid, Material markerMaterial) : IDisposable
{
    private const float ArrowWidth = 0.3f;
    private const float HeadLength = 1.4f;

    // How far each wing of the arrowhead turns from the shaft.
    private const float HeadAngle = math.PI / 6f;

    private readonly LiveSingleton<FacingDragState> _drag = new();
    private readonly LiveSingleton<NavGrid> _grid = new();
    private readonly GroundMarkers _markers = new GroundMarkers(grid, markerMaterial);

    private readonly LiveQuery _units = new(entityManager => entityManager.CreateEntityQuery(
        ComponentType.ReadOnly<Selected>(),
        ComponentType.ReadOnly<Owner>(),
        ComponentType.ReadOnly<NavAgent>(),
        ComponentType.ReadOnly<LocalTransform>()));

    public void Gather(MatchView view, OverlayStyle style)
    {
        var entityManager = view.EntityManager;
        if (!view.HasMap || !_drag.TryGet(entityManager, out var drag) || !drag.Aimed)
        {
            return;
        }

        AddArrow(drag, style.rally);
        _grid.Complete(entityManager);
        if (_grid.TryGet(entityManager, out var navGrid))
        {
            AddSlots(view, drag, navGrid, style);
        }
    }

    public void Draw(int layer) => _markers.Draw(layer);

    public void Dispose() => _markers.Dispose();

    /// <summary>The slots a move to the drag's press point facing along it gives <paramref name="members"/>.</summary>
    public static void Slots(NativeArray<FormationMember> members, in FacingDragState drag, in MapSettings map,
        in NavGrid navGrid, NativeArray<float3> slots) =>
        new FormationSlots(members).Assign(drag.Press, drag.Facing, map, navGrid, slots);

    private void AddSlots(MatchView view, in FacingDragState drag, in NavGrid navGrid, OverlayStyle style)
    {
        var entityManager = view.EntityManager;
        var query = _units.In(entityManager);
        using var units = query.ToEntityArray(Allocator.Temp);
        using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
        using var agents = query.ToComponentDataArray<NavAgent>(Allocator.Temp);
        using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var members = new NativeList<FormationMember>(units.Length, Allocator.Temp);
        var spread = 0f;
        for (var i = 0; i < units.Length; i++)
        {
            if (owners[i].Value != view.Local.Owner)
            {
                continue;
            }

            if (entityManager.HasBuffer<SquadMember>(units[i]))
            {
                spread = new SquadRoster(entityManager.GetBuffer<SquadMember>(units[i], true)).Spread();
            }

            members.Add(new FormationMember(transforms[i].Position, agents[i], spread));
            spread = 0f;
        }

        if (members.Length == 0)
        {
            return;
        }

        using var slots = new NativeArray<float3>(members.Length, Allocator.Temp);
        Slots(members.AsArray(), drag, view.Map, navGrid, slots);
        for (var i = 0; i < members.Length; i++)
        {
            var footprint = MarkerFootprint.Agent(members[i].Agent, style.markerMargin);
            _markers.Add(new MarkerPose(slots[i], drag.Yaw), footprint, style.rally, style.Outline);
        }
    }

    private void AddArrow(in FacingDragState drag, Color color)
    {
        var tip = drag.Pointer;
        var back = new float3(-drag.Facing.x, 0f, -drag.Facing.y);
        AddBar(drag.Press, tip, color);
        AddBar(tip, tip + math.mul(quaternion.RotateY(HeadAngle), back) * HeadLength, color);
        AddBar(tip, tip + math.mul(quaternion.RotateY(-HeadAngle), back) * HeadLength, color);
    }

    private void AddBar(float3 from, float3 to, Color color)
    {
        var length = math.distance(from.xz, to.xz);
        var yaw = math.atan2(to.x - from.x, to.z - from.z);
        var footprint = new MarkerFootprint(new float2(ArrowWidth * 0.5f, length * 0.5f), ArrowWidth * 0.5f,
            MarkerShape.Outline);
        var look = new MarkerLook(ArrowWidth, ArrowWidth, 0f);
        _markers.Add(new MarkerPose((from + to) * 0.5f, yaw), footprint, color, look);
    }
}
