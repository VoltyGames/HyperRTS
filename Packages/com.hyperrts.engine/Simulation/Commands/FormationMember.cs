using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Commands;

/// <summary>One unit in a formation: where it stands, what it is to the map and how much room its slot takes.</summary>
public readonly struct FormationMember
{
    public readonly float3 Position;
    public readonly NavAgent Agent;
    public readonly float2 SlotSpacing;

    /// <summary>A squad's slot holds its members too, so <paramref name="squadSpread"/> keeps them clear of the slots beside it.</summary>
    public FormationMember(float3 position, in NavAgent agent, float squadSpread)
    {
        Position = position;
        Agent = agent;
        SlotSpacing = agent.SlotSpacing + squadSpread * 2f;
    }

    /// <summary>The nearest spot to the slot the unit fits and can reach, so no slot lands in a building.</summary>
    public float3 Fit(float3 slot, in NavGrid grid)
    {
        // Aircraft fly over everything.
        if (Agent.Layer == NavLayer.Air)
        {
            return slot;
        }

        // A unit spawned this frame has no class until the field system runs; the pathfinder fits it then.
        if (!Agent.TryAccess(grid, out var access))
        {
            return slot;
        }

        var cell = grid.Grid.Cell(slot);
        if (!access.TryFindNearest(cell, GridPathfinder.NearestSearchRadius, out var found) || found.Equals(cell))
        {
            return slot;
        }

        // Ground the unit can't reach is left to the pathfinder, which snaps the goal into the unit's region.
        var center = grid.Grid.CenterPoint(found);
        var fitted = new float3(center.x, slot.y, center.z);
        return grid.IsReachable(Agent, Position, fitted) ? fitted : slot;
    }
}
