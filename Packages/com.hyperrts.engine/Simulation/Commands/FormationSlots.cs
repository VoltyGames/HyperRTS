using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Commands;

/// <summary>
/// Where a group would stand for a ground move: <see cref="Formation"/> over the members' positions and sizes, each
/// slot clamped to the map and fitted to ground its unit can reach. The order and the facing preview both read it.
/// </summary>
public readonly struct FormationSlots(NativeArray<FormationMember> members)
{
    /// <summary>
    /// Writes each member's fitted slot into <paramref name="slots"/> and returns the group's extent: how far apart
    /// the slots lie before fitting.
    /// </summary>
    public float Assign(float3 goal, float2 facing, in MapSettings map, in NavGrid grid, NativeArray<float3> slots)
    {
        var positions = new NativeArray<float3>(members.Length, Allocator.Temp);
        var spacings = new NativeArray<float2>(members.Length, Allocator.Temp);
        for (var i = 0; i < members.Length; i++)
        {
            positions[i] = members[i].Position;
            spacings[i] = members[i].SlotSpacing;
        }

        Formation.Assign(positions, goal, facing, spacings, slots);
        var extent = Formation.Extent(slots, goal);
        for (var i = 0; i < members.Length; i++)
        {
            slots[i] = members[i].Fit(map.Clamp(slots[i]), grid);
        }

        return extent;
    }
}
