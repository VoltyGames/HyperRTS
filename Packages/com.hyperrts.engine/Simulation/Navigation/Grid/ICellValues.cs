using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>A value per grid cell, so one bilinear sampler serves the field and its windows.</summary>
    internal interface ICellValues
    {
        float At(int2 cell);
    }
}
