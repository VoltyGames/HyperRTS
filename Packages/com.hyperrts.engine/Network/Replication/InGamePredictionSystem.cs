using HyperRTS.Network.Common;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Replication
{
    /// <summary>
    /// Client: predicts only while the connection is in game, since predicting a ghost after a drop throws (and aborts a
    /// player build under Burst). Not Burst: it toggles a managed group.
    /// </summary>
    [RemoteClientOnly]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial struct InGamePredictionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var prediction = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
            prediction.Enabled = SystemAPI.HasSingleton<NetworkStreamInGame>();
        }
    }
}
