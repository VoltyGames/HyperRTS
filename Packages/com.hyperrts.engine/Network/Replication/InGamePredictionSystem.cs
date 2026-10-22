using HyperRTS.Network.Common;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Replication
{
    /// <summary>
    /// Client: predicts only while the connection is in game. The frame a connection drops, Netcode clears the ghost
    /// collection but despawns the ghosts a frame later, and predicting a ghost in between throws (in a player
    /// build, Burst aborts the process). Not Burst: it toggles a managed group.
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
