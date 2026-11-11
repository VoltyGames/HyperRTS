using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Logs one error naming the map when a match with AI players has no <see cref="AIMapHints"/> (or lacks an AI
    /// player's slot), since the skirmish AI plays only on maps that carry them.
    /// </summary>
    [UpdateInGroup(typeof(AISystemGroup), OrderFirst = true)]
    public partial struct AIMapHintsCheckSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var bots = SystemAPI.QueryBuilder().WithAll<AIPlayer>().Build();
            state.RequireForUpdate(bots);
            state.RequireForUpdate<MapSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;
            var map = SceneManager.GetActiveScene().name;
            if (!SystemAPI.HasSingleton<AIMapHints>())
            {
                Debug.LogError($"Map '{map}' has no AI map hints, so its AI players stay idle. Rebuild the map: its builder writes the hints (expansions, attack lanes, defence points).");
                return;
            }

            var hints = SystemAPI.GetSingletonEntity<AIMapHints>();
            var view = new AIMapHintsView(SystemAPI.GetBuffer<AISlotHint>(hints),
                SystemAPI.GetBuffer<AILaneHint>(hints), SystemAPI.GetBuffer<AIHintPoint>(hints));
            foreach (var player in SystemAPI.Query<RefRO<Player>>().WithAll<AIPlayer>())
            {
                if (!view.TryGetSlot(player.ValueRO.Owner, out _))
                {
                    Debug.LogError($"Map '{map}' has no AI map hints for slot {player.ValueRO.Owner}, so that AI player stays idle.");
                }
            }
        }
    }
}
