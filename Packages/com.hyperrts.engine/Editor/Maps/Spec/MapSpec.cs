using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A map as authored: one symmetry sector of features that <see cref="MapSymmetry"/> copies to fill the map.
    /// Read from <c>&lt;Map&gt;.map.json</c> with JsonUtility; the map skill's reference describes the format.
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
        public List<string> notes = new();
        public List<MapSpawnSpec> spawns = new();
        public List<MapResourceSpec> resources = new();
        public List<MapPropSpec> props = new();
        public List<MapRiverSpec> rivers = new();
        public List<MapRavineSpec> ravines = new();
        public List<MapHillSpec> hills = new();
        public List<MapRoadSpec> roads = new();
        public List<MapSettlementSpec> settlements = new();
        public List<MapWoodSpec> woods = new();
        public List<MapBeltSpec> belts = new();
        public List<MapFarmlandSpec> farmland = new();
        public List<MapAreaSpec> areas = new();
        public List<MapLineSpec> lines = new();
        public List<MapScatterSpec> scatters = new();

        public MapWarAge WarAge => Enum.Parse<MapWarAge>(warAge);
    }
}
