using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// The skirmish AI plays only on maps that carry <see cref="AIMapHints"/>. Once a match has a map and AI players,
    /// this logs one error naming the map when the hints are missing (or lack an AI player's slot) instead of the
    /// bots quietly doing something else.
    /// </summary>
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
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
            var slots = SystemAPI.GetBuffer<AISlotHint>(hints);
            foreach (var player in SystemAPI.Query<RefRO<Player>>().WithAll<AIPlayer>())
            {
                if (!HasSlot(slots, player.ValueRO.Faction))
                {
                    Debug.LogError($"Map '{map}' has no AI map hints for slot {player.ValueRO.Faction}, so that AI player stays idle.");
                }
            }
        }

        private static bool HasSlot(DynamicBuffer<AISlotHint> slots, byte faction)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].Slot == faction)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
