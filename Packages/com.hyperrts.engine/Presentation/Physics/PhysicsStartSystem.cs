using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>
    /// Steps the main physics world once, which a <see cref="CustomPhysicsSystemGroup"/> needs before it runs, then
    /// switches it off: no gameplay entity has physics, so only <see cref="PhysicsGroup"/> steps. Not Burst: it toggles
    /// managed groups.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PhysicsStartSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<SimulationSingleton>();

        protected override void OnUpdate()
        {
            var main = World.GetExistingSystemManaged<PhysicsSystemGroup>();
            World.PushTime(new Unity.Core.TimeData(0.0, 0f));
            main.Update();
            World.PopTime();
            main.Enabled = false;
            // A -nographics client shows nothing that moves in it.
            World.GetExistingSystemManaged<PhysicsGroup>().Enabled = GraphicsSupport.Available;
            Enabled = false;
        }
    }
}
