using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>What a side (the game's army id, see <see cref="PlayerSlot.side"/>) starts with and how its AI opens.</summary>
    [Serializable]
    public class SideContent
    {
        [Tooltip("The game's army id; above 0.")]
        public byte side = 1;

        [Tooltip("Spawned at each of this side's players' spawns, owned by the player.")]
        public List<StartKitItem> startKit = new();

        [Tooltip("AI opening of this side's bots; empty trains freely from the start.")]
        public AIBuildOrder buildOrder;

        public void Bake(IBaker baker, Entity match)
        {
            baker.AppendToBuffer(match, new MatchSide { Value = side });
            for (var i = 0; i < startKit.Count; i++)
            {
                var item = startKit[i];
                if (item.prefab == null)
                {
                    throw new ArgumentException($"Side {side}'s start kit item {i} has no prefab.");
                }

                baker.AppendToBuffer(match, new SideStartItem
                {
                    Side = side,
                    Prefab = baker.GetEntity(item.prefab, TransformUsageFlags.Dynamic),
                    Offset = item.offset,
                    Yaw = math.radians(item.yaw),
                    FacesCamera = item.facesCamera,
                });
            }

            if (buildOrder == null)
            {
                return;
            }

            foreach (var (prefab, count) in buildOrder.BakeSteps(baker))
            {
                baker.AppendToBuffer(match, new SideBuildStep { Side = side, Prefab = prefab, Count = count });
            }
        }
    }
}
