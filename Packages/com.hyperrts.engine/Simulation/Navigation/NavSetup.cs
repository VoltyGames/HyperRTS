using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Adds the components map features give the nav grid: areas, obstacles and the terrain heightfield.</summary>
    public static class NavSetup
    {
        public static void AddArea<TSink>(ref TSink sink, float2 size, NavAreaKind kind)
            where TSink : struct, IComponentSink =>
            sink.Add(new NavArea { Size = size, Kind = kind });

        public static void AddObstacle<TSink>(ref TSink sink, float2 size) where TSink : struct, IComponentSink =>
            sink.Add(new NavObstacle { Size = size });

        /// <summary>One per match; the caller owns the heightfield's blob.</summary>
        public static void AddTerrain<TSink>(ref TSink sink, in TerrainHeight height)
            where TSink : struct, IComponentSink =>
            sink.Add(height);
    }
}
