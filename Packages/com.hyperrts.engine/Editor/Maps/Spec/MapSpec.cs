using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec;

/// <summary>
/// A map as authored: one symmetry sector of features that <see cref="MapSymmetry"/> copies to fill the map.
/// Read from <c>&lt;Map&gt;.map.json</c> with JsonUtility; the format is in docs/maps/README.md, under Spec format.
/// </summary>
[Serializable]
public sealed class MapSpec
{
    public string name;
    public string displayName;
    public string mode;
    public string mapType;
    [MapKey(typeof(MapClimate))]
    public string climate;

    /// <summary>The climate weather the scene is built with; a match may pick another the climate offers.</summary>
    public string weather;

    /// <summary>How scarred the land is before the battle, a <see cref="MapWarAge"/> name.</summary>
    public string warAge = nameof(MapWarAge.None);
    public MapPoint size;
    public string symmetry;
    public int seed;
    public MapTerrainSpec terrain = new();
    public string status;

    /// <summary>Whether players may pick the map. An unlisted one, a test or showroom ground, plays only from its scene.</summary>
    public bool listed = true;
    public List<string> notes = [];
    public List<MapSpawnSpec> spawns = [];
    public List<MapResourceSpec> resources = [];
    public List<MapPropSpec> props = [];
    public List<MapRiverSpec> rivers = [];
    public List<MapPondSpec> ponds = [];
    public List<MapRavineSpec> ravines = [];
    public List<MapHillSpec> hills = [];
    public List<MapRoadSpec> roads = [];
    public List<MapSettlementSpec> settlements = [];
    public List<MapWoodSpec> woods = [];
    public List<MapBeltSpec> belts = [];
    public List<MapFarmlandSpec> farmland = [];
    public List<MapAreaSpec> areas = [];
    public List<MapRowSpec> rows = [];
    public List<MapScatterSpec> scatters = [];

    /// <summary>Points the Play-mode look captures frame (<c>map look</c>); they change nothing on the map.</summary>
    public List<MapLookSpec> looks = [];

    public MapWarAge WarAge => Enum.Parse<MapWarAge>(warAge);
}
