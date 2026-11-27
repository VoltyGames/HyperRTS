using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation.Areas;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Palette;

/// <summary>A bridge's roadway as plain numbers, so planners on worker threads never touch its prefab.</summary>
public readonly struct MapDeck
{
    /// <summary>Width across by length along, from the prefab's Deck Nav Area.</summary>
    public readonly Vector2 Size;

    /// <summary>How far the building footprint runs over the approach slabs, past each end of the deck.</summary>
    public readonly float Landing;

    /// <summary>Length between the abutment faces, measured from the abutments its Nav Area lists (<see cref="NavAreaAuthoring.abutments"/>).</summary>
    public readonly float ClearSpan;

    /// <summary>Each pier standing in the water as (x along the river, z across it, half-width across the flow), from the deck's middle (<see cref="NavAreaAuthoring.piers"/>).</summary>
    public readonly Vector3[] Piers;

    private MapDeck(Vector2 size, float landing, float clearSpan, Vector3[] piers)
    {
        Size = size;
        Landing = landing;
        ClearSpan = clearSpan;
        Piers = piers;
    }

    /// <summary>Whether the abutments stand inside the deck, as the height shaper needs.</summary>
    public bool HasAbutments => ClearSpan > 0f && ClearSpan < Size.y;

    /// <summary>From the deck's middle to the end of its approach slab.</summary>
    public float Reach => Size.y * 0.5f + Landing;

    /// <summary>Reads a bridge prop's prefab on the main thread.</summary>
    public static MapDeck Read(MapProp entry)
    {
        var prefab = entry.prefab;
        var area = prefab.GetComponent<NavAreaAuthoring>();
        var size = area.size;
        var clearSpan = area.ClearSpan();
        var piers = area.PierPoints();
        var landing = prefab.TryGetComponent<BuildingAuthoring>(out var building)
            ? Mathf.Max(0f, (building.footprint.y - size.y) * 0.5f)
            : 0f;
        return new MapDeck(size, landing, clearSpan, piers);
    }
}
