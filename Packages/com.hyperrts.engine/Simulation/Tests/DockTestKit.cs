using HyperRTS.Simulation.Navigation.Docks;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Dock bays the dock and traffic tests share.</summary>
    public static class DockTestKit
    {
        /// <summary>A bay stopping at <paramref name="stop"/>, facing <paramref name="facing"/>, with the default run-in and exit.</summary>
        public static DockBay Bay(float3 stop, float2 facing) => new()
        {
            Stop = stop, Facing = facing, RunIn = DockAuthoring.DefaultRunIn,
            Exit = stop + new float3(facing.x, 0f, facing.y) * DockAuthoring.DefaultExit,
        };
    }
}
