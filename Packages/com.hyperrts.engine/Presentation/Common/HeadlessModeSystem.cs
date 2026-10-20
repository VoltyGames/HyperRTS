using HyperRTS.Core;
using Unity.Entities;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Switches the world's <see cref="PresentationSystemGroup"/> off when there is no graphics device
    /// (<see cref="GraphicsSupport"/>), so a headless client only simulates and networks. Not Burst: it toggles a
    /// managed group.
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
