using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>Samples the terrain height at each sight cell's centre.</summary>
    [BurstCompile]
    internal struct SightGroundJob : IJobParallelFor
    {
        public NativeArray<SightCell> Cells;
        public CellGrid Grid;
        public TerrainHeight Terrain;

        public void Execute(int index)
        {
            var cell = Cells[index];
            cell.Ground = Terrain.Height(Grid.Center(Grid.Cell(index)));
            Cells[index] = cell;
        }
    }
}
