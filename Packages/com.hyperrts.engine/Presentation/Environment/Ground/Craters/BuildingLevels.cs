using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ground.Stamps;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground.Craters;

/// <summary>The buildings the drawn terrain is levelled under, and the ground their changes touch.</summary>
public sealed class BuildingLevels
{
    private readonly Dictionary<Entity, BuildingLevel> _current = new();
    private readonly List<BuildingLevel> _list = new();
    private float2 _filledMin = new(float.MaxValue);
    private float2 _filledMax = new(float.MinValue);

    public List<BuildingLevel> All => _list;

    /// <summary>
    /// Swaps in the wanted set and grows the XZ box to cover every building that came, went or changed; true when any did.
    /// Each new or changed building also levels the tracks map's ground under it, through <paramref name="stamps"/>.
    /// </summary>
    public bool Replace(Dictionary<Entity, BuildingLevel> wanted, TrampleStamps stamps, ref float2 min, ref float2 max)
    {
        var changed = false;
        foreach (var (entity, level) in _current)
        {
            if (!wanted.TryGetValue(entity, out var next) || !next.Equals(level))
            {
                Cover(level, ref min, ref max);
                changed = true;
            }
        }

        foreach (var (entity, level) in wanted)
        {
            if (!_current.TryGetValue(entity, out var last) || !last.Equals(level))
            {
                Cover(level, ref min, ref max);
                Cover(level, ref _filledMin, ref _filledMax);
                stamps.AddFill(level.Area, BuildingLevel.Skirt);
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        _current.Clear();
        _list.Clear();
        foreach (var (entity, level) in wanted)
        {
            _current[entity] = level;
            _list.Add(level);
        }

        return true;
    }

    /// <summary>
    /// Grows the box over the buildings levelled last frame, whose fills the tracks map has drawn since, and starts over.
    /// The terrain redraws there so it matches the map.
    /// </summary>
    public void TakeFilled(ref float2 min, ref float2 max)
    {
        min = math.min(min, _filledMin);
        max = math.max(max, _filledMax);
        ResetFilled();
    }

    public void Clear()
    {
        _current.Clear();
        _list.Clear();
        ResetFilled();
    }

    private void ResetFilled()
    {
        _filledMin = new float2(float.MaxValue);
        _filledMax = new float2(float.MinValue);
    }

    private static void Cover(in BuildingLevel level, ref float2 min, ref float2 max)
    {
        min = math.min(min, level.Area.Center - level.Reach);
        max = math.max(max, level.Area.Center + level.Reach);
    }
}
