using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Geometry;
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
/// While a move is dragged to aim its facing: a line across the formation's front with a chevron pointing the way,
/// and faint brackets round each selected unit's slot, laid out by the same <see cref="FormationSlots"/> the order
/// uses. The drag turns it and never stretches it.
/// </summary>
public sealed class FacingPreview(Mesh grid, Material markerMaterial) : IDisposable
{
    private const float LineWidth = 0.35f;

    // Metres between the front row's leading edge and the line.
    private const float LineGap = 1.5f;

    private const float ChevronLength = 1.6f;

    // How far each side of the chevron turns back from the facing.
    private const float ChevronAngle = math.PI / 4f;

    // Slot brackets show where units stand without competing with the line.
    private const float SlotOpacity = 0.55f;

    private const float BracketCorner = 0.25f;

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

        _grid.Complete(entityManager);
        if (_grid.TryGet(entityManager, out var navGrid))
        {
            AddFormation(view, drag, navGrid, style);
        }
    }

    public void Draw(int layer) => _markers.Draw(layer);

    public void Dispose() => _markers.Dispose();

    private void AddFormation(MatchView view, in FacingDragState drag, in NavGrid navGrid, OverlayStyle style)
    {
        using var members = Members(view);
        if (members.Length == 0)
        {
            return;
        }

        using var slots = new NativeArray<float3>(members.Length, Allocator.Temp);
        new FormationSlots(members.AsArray()).Assign(drag.Press, drag.Facing, view.Map, navGrid, slots);
        var yaw = drag.Yaw;
        var faint = style.rally;
        faint.a *= SlotOpacity;
        var front = new FrontEdge(drag);
        for (var i = 0; i < members.Length; i++)
        {
            var outline = MarkerFootprint.Agent(members[i].Agent, style.markerMargin);
            var brackets = new MarkerFootprint(outline.HalfSize, BracketCorner, MarkerShape.Brackets);
            _markers.Add(new MarkerPose(slots[i], yaw), brackets, faint, style.Outline);
            front.Include(slots[i], outline.HalfSize);
        }

        AddFront(front, drag, style.rally);
    }

    private NativeList<FormationMember> Members(MatchView view)
    {
        var entityManager = view.EntityManager;
        var query = _units.In(entityManager);
        using var units = query.ToEntityArray(Allocator.Temp);
        using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
        using var agents = query.ToComponentDataArray<NavAgent>(Allocator.Temp);
        using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var members = new NativeList<FormationMember>(units.Length, Allocator.Temp);
        for (var i = 0; i < units.Length; i++)
        {
            if (owners[i].Value != view.Local.Owner)
            {
                continue;
            }

            var spread = entityManager.HasBuffer<SquadMember>(units[i])
                ? new SquadRoster(entityManager.GetBuffer<SquadMember>(units[i], true)).Spread()
                : 0f;
            members.Add(new FormationMember(transforms[i].Position, agents[i], spread));
        }

        return members;
    }

    private void AddFront(in FrontEdge front, in FacingDragState drag, Color color)
    {
        var left = front.Point(front.Left, LineGap);
        var right = front.Point(front.Right, LineGap);
        AddBar(left, right, color);

        var tip = front.Point((front.Left + front.Right) * 0.5f, LineGap + ChevronLength);
        AddBar(tip, tip + ChevronSide(-drag.Facing, ChevronAngle), color);
        AddBar(tip, tip + ChevronSide(-drag.Facing, -ChevronAngle), color);
    }

    private static float3 ChevronSide(float2 back, float angle)
    {
        var side = Planar.Rotate(back, angle) * ChevronLength;
        return new float3(side.x, 0f, side.y);
    }

    private void AddBar(float3 from, float3 to, Color color)
    {
        var length = math.distance(from.xz, to.xz);
        var yaw = math.atan2(to.x - from.x, to.z - from.z);
        var half = LineWidth * 0.5f;
        var footprint = new MarkerFootprint(new float2(half, length * 0.5f + half), half, MarkerShape.Outline);
        var look = new MarkerLook(LineWidth, LineWidth, 0f);
        _markers.Add(new MarkerPose((from + to) * 0.5f, yaw), footprint, color, look);
    }
}
