using System.Collections.Generic;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements;

/// <summary>
/// Fills an accepted plot with house, outbuildings, orchard, extras and fences, chosen at authored-sector points
/// and placed in every copy.
/// </summary>
internal sealed class MapPlotBuilder
{
    private const int Tries = 10;
    private const float EdgeMargin = 0.5f;
    private readonly MapSettlement _settlement;
    private readonly MapPlanContext _context;
    private readonly MapSettlementKit _kit;
    private readonly MapSettlementEntries _entries;
    private readonly MapPlotFences _fences;
    private readonly MapPlotPaths _paths;
    private readonly List<Vector2> _spots = [];
    private readonly System.Random _random;

    public MapPlotBuilder(MapSettlement settlement, System.Random random)
    {
        _settlement = settlement;
        _context = settlement.Context;
        _kit = settlement.Kit;
        _entries = settlement.Entries;
        _fences = new MapPlotFences(settlement);
        _paths = new MapPlotPaths(_context, _kit);
        _random = random;
    }

    private MapClearance Clearance => _context.Placements.Clearance;

    /// <summary>Picks the plot's house, or returns null with the reason the plot is unusable. Nothing is placed yet.</summary>
    public MapPlacement Choose(MapPlot plot, out string problem)
    {
        var entry = _entries.Houses[_random.Next(_entries.Houses.Count)];
        var scale = Scale(entry);
        var width = entry.size.x * scale;
        var depth = entry.size.y * scale;
        if (width > plot.Width - 2f || _kit.frontYard + depth > plot.Depth * 0.7f)
        {
            problem = "too small for the house";
            return null;
        }

        var slack = (plot.Width - width) * 0.5f - 1f;
        var u = _random.Signed() * slack * 0.3f;
        var position = plot.Point(u, _kit.frontYard + depth * 0.5f);
        if (!Clearance.IsFree(position, entry.Radius * scale))
        {
            problem = "blocked by something already placed";
            return null;
        }

        problem = null;
        return new MapPlacement(entry, position, plot.FacingYaw + Jitter(entry), 1f) { Scale = scale };
    }

    /// <summary>
    /// Places the house and outbuildings, then fixes the plot's zones: the yard ends behind the deepest of them,
    /// so room kept for an outbuilding that didn't fit goes to the garden.
    /// </summary>
    public void BuildYard(MapPlot plot, MapPlacement house)
    {
        _spots.Clear();
        Add(house.Entry, house.Position, house.Yaw, house.Scale);
        var houseBack = _kit.frontYard + house.Entry.size.y * house.Scale;
        plot.YardEnd = Outbuildings(plot, houseBack);
        plot.GardenEnd = plot.YardEnd + _kit.gardenShare * (plot.Depth - plot.YardEnd);
    }

    /// <summary>Places the orchard, extras and fences on a plot whose yard is built and land claimed, and wears its paths.</summary>
    public void Furnish(MapPlot plot, MapPlacement house, int seed)
    {
        Orchard(plot);
        Extras(plot);
        plot.Local(house.Position, out var houseU, out _);
        var gate = _fences.Build(plot, houseU) ?? plot.Point(houseU, 0f);
        _paths.Wear(plot, house, gate, _spots, seed);
        _paths.MarkDoor(plot, house);
    }

    /// <summary>Places the outbuildings and returns the depth of the back of the yard.</summary>
    private float Outbuildings(MapPlot plot, float houseBack)
    {
        var yardBack = houseBack;
        if (_entries.Outbuildings.Count == 0)
        {
            return yardBack;
        }

        var room = Mathf.Min(plot.Depth, houseBack + _entries.OutbuildingDepth);
        var placed = 0;
        for (var attempt = 0; attempt < Tries * _kit.maxOutbuildings && placed < _kit.maxOutbuildings; attempt++)
        {
            if (TryOutbuilding(plot, room, out var back))
            {
                yardBack = Mathf.Max(yardBack, back);
                placed++;
            }
        }

        return yardBack;
    }

    /// <summary>
    /// One try at a spot beside or behind the house, level with its front or further back but within
    /// <paramref name="room"/>; <paramref name="back"/> is the depth of its back wall.
    /// </summary>
    private bool TryOutbuilding(MapPlot plot, float room, out float back)
    {
        back = 0f;
        var entry = _entries.Outbuildings[_random.Next(_entries.Outbuildings.Count)];
        var scale = Scale(entry);
        var halfWidth = entry.size.x * scale * 0.5f;
        var halfDepth = entry.size.y * scale * 0.5f;
        var reach = plot.Width * 0.5f - halfWidth - 1f;
        if (!Between(-reach, reach, out var u))
        {
            return false;
        }

        if (!Between(_kit.frontYard + halfDepth, room - halfDepth, out var v))
        {
            return false;
        }

        var position = plot.Point(u, v);
        if (!Clearance.IsFree(position, entry.Radius * scale))
        {
            return false;
        }

        Add(entry, position, plot.FacingYaw + Jitter(entry), scale);
        _spots.Add(position);
        back = v + halfDepth;
        return true;
    }

    /// <summary>Orchard trees on a jittered grid across the back of the plot.</summary>
    private void Orchard(MapPlot plot)
    {
        var tree = _entries.OrchardTree;
        if (tree < 0)
        {
            return;
        }

        var trees = _context.Trees;
        var spacing = _kit.orchardSpacing;
        var half = plot.Width * 0.5f;
        for (var v = plot.GardenEnd + spacing * 0.5f; v < plot.Depth; v += spacing)
        {
            for (var u = -half + spacing * 0.5f; u < half; u += spacing)
            {
                var along = u + _random.Signed() * spacing * 0.25f;
                var back = v + _random.Signed() * spacing * 0.25f;
                var point = plot.Point(along, back);
                var scale = trees.Scale(tree, _random);
                var yaw = _random.Yaw();
                if (plot.Contains(point, 1f) && trees.Fits(point, spacing * 0.4f))
                {
                    trees.PlantCopies(tree, point, scale, yaw);
                }
            }
        }
    }

    private void Extras(MapPlot plot)
    {
        foreach (var (entry, prop) in _entries.Extras)
        {
            if (_random.NextDouble() >= prop.chance)
            {
                continue;
            }

            for (var attempt = 0; attempt < Tries; attempt++)
            {
                if (TryExtra(plot, entry, prop.zone))
                {
                    break;
                }
            }
        }
    }

    private bool TryExtra(MapPlot plot, MapProp entry, MapPlotZone zone)
    {
        var scale = Scale(entry);
        var radius = entry.Radius * scale;
        var reach = plot.Width * 0.5f - radius - EdgeMargin;
        if (!Between(-reach, reach, out var u))
        {
            return false;
        }

        ZoneSpan(plot, zone, radius, out var from, out var to);
        if (!Between(from, to, out var v))
        {
            return false;
        }

        var position = plot.Point(u, v);
        if (!Clearance.IsFree(position, radius))
        {
            return false;
        }

        if (zone == MapPlotZone.Street && !StreetsideFree(position, radius))
        {
            return false;
        }

        Add(entry, position, plot.FacingYaw + Jitter(entry), scale);
        if (zone is MapPlotZone.Yard or MapPlotZone.Garden)
        {
            _spots.Add(position);
        }

        return true;
    }

    /// <summary>The depths a prop of a given radius may stand at in a zone; the street is the verge in front.</summary>
    private void ZoneSpan(MapPlot plot, MapPlotZone zone, float radius, out float from, out float to)
    {
        switch (zone)
        {
            case MapPlotZone.Street:
                from = to = -_kit.frontGap * 0.5f;
                return;
            case MapPlotZone.Yard:
                from = radius;
                to = plot.YardEnd - radius;
                return;
            case MapPlotZone.Garden:
                from = plot.YardEnd + radius;
                to = plot.GardenEnd - radius;
                return;
            default:
                from = plot.GardenEnd + radius;
                to = plot.Depth - radius;
                return;
        }
    }

    private bool StreetsideFree(Vector2 position, float radius) =>
        Clearance.IsDry(position, radius) && _settlement.OnVerge(position);

    private void Add(MapProp entry, Vector2 position, float yaw, float scale) =>
        _context.Placements.AddEverywhere(entry, position, yaw, scale, 1f);

    private float Scale(MapProp entry) => _random.Range(entry.scale.x, entry.scale.y);

    private float Jitter(MapProp entry) => entry.yawJitter * _random.Signed();

    /// <summary>A random value in a range; false when the range is empty.</summary>
    private bool Between(float min, float max, out float value)
    {
        value = 0f;
        if (max < min)
        {
            return false;
        }

        value = _random.Range(min, max);
        return true;
    }
}
