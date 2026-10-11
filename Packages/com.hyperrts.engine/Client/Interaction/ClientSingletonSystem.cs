using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Client.Interaction
{
    /// <summary>
    /// Creates the client-side singletons that input, HUD and overlays share, once per presented world, so no layer
    /// depends on which of them starts first.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct ClientSingletonSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            entityManager.CreateSingleton<SelectionDragState>();
            entityManager.CreateSingleton<PendingCommand>();
            entityManager.CreateSingleton<PlacementState>();
            entityManager.CreateSingleton<PointerState>();
            entityManager.CreateSingleton<CameraFocusRequest>();
            entityManager.CreateSingleton<HotkeyPresses>();
            state.Enabled = false;
        }
    }
}
