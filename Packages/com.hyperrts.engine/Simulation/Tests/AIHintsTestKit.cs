using System;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The AI map hints a test world carries, which the skirmish AI needs to play.</summary>
    public static class AIHintsTestKit
    {
        /// <summary>Makes the hints singleton from <paramref name="data"/>, replacing the world's current one.</summary>
        public static Entity CreateHints(this TestWorld world, AIMapHintsData data)
        {
            world.RemoveHints();
            var entity = world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            AIMapHintsAuthoring.Add(ref writer, data);
            return entity;
        }

        public static void RemoveHints(this TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(AIMapHints));
            world.EntityManager.DestroyEntity(query);
        }

        /// <summary>A slot per team entry (faction = index + 1) and one lane without waypoints between each pair of enemies.</summary>
        public static AIMapHintsData DirectHints(params byte[] teams)
        {
            var data = new AIMapHintsData();
            for (var i = 0; i < teams.Length; i++)
            {
                data.AddSlot(i + 1, Array.Empty<Vector2>(), Array.Empty<Vector2>());
            }

            for (var from = 0; from < teams.Length; from++)
            {
                for (var to = 0; to < teams.Length; to++)
                {
                    if (teams[from] != teams[to])
                    {
                        data.AddLane(from + 1, to + 1, Array.Empty<Vector2>());
                    }
                }
            }

            return data;
        }
    }
}
