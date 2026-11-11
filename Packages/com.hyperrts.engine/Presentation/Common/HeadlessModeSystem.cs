using HyperRTS.Simulation.Common.Systems;
using Unity.Entities;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Switches the <see cref="PresentationSystemGroup"/> off without a graphics device (<see cref="GraphicsSupport"/>).
    /// Not Burst: it toggles a managed group.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct HeadlessModeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
            if (GraphicsSupport.Available)
            {
                return;
            }

            state.World.GetOrCreateSystemManaged<PresentationSystemGroup>().Enabled = false;
        }
    }
}
