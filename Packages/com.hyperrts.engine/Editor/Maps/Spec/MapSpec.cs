using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A map as authored: one symmetry sector of features that <see cref="MapSymmetry"/> copies to fill the map.
    /// Read from <c>&lt;Map&gt;.map.json</c> with JsonUtility; the map skill's reference describes the format.
    /// </summary>
    [Serializable]
    public sealed class MapSpec
    {
        public const int CurrentFormat = 1;

        public int formatVersion;
        public string name;
        public string displayName;
        public string mode;
        public string mapType;
        public string climate;
        public MapPoint size;
        public string symmetry;
        public int seed;
        public MapTerrainSpec terrain = new();
        public string status;
        public List<string> notes = new();
        public List<MapSpawnSpec> spawns = new();
        public List<MapResourceSpec> resources = new();
        public List<MapPropSpec> props = new();
        public List<MapTownSpec> towns = new();
        public List<MapAreaSpec> areas = new();
        public List<MapRoadSpec> roads = new();
        public List<MapRiverSpec> rivers = new();
        public List<MapHillSpec> hills = new();
        public List<MapRavineSpec> ravines = new();
        public List<MapLineSpec> lines = new();
        public List<MapScatterSpec> scatters = new();
    }
}
