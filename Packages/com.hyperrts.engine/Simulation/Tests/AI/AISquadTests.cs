using System;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.AI
{
    /// <summary>
    /// Skirmish AI squad play: retreating pinned or worn squads, reinforcing at a point and sitting out attacks until
    /// whole, and digging field works in at defence points once the economy is up.
    /// </summary>
    public class AISquadTests : SimulationFixture
    {
        private const float Each = 25f;
        private const byte Bot = 2;

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
            AIPlayerSetup.Add(World.Writer(World.Player(Bot)), new AIPlayer
            {
                ThinkInterval = 0.5f, AttackWaveSize = 50, RetreatsSquads = true, RetreatLivingShare = 0.5f,
                RetreatHealthShare = 0.33f,
            });
        }

        private Entity Squad(float3 position, int dead = 0)
        {
            var squad = World.SpawnUnit(Bot, position, speed: 5f, maxHealth: Each * 4);
            SquadAuthoring.Add(World.Writer(squad), Each * 4, new byte[4]);
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            var members = World.Buffer<SquadMember>(squad);
            for (var i = 0; i < dead; i++)
            {
                var member = members[members.Length - 1 - i];
                member.Health = 0f;
                members[members.Length - 1 - i] = member;
            }

            return squad;
        }

        private void RetreatPointAt(float3 position)
        {
            var point = World.SpawnBuilding(Bot, position, new float2(4f, 4f));
            World.Add(point, new RetreatPoint { Radius = 8f });
        }

        private bool IsRetreating(Entity squad) =>
            World.IsEnabled<ActiveOrder>(squad) && World.Get<ActiveOrder>(squad).Value.Type == OrderType.Retreat;

        [Test]
        public void PinnedSquad_RetreatsToAFriendlyPoint()
        {
            var squad = Squad(float3.zero);
            World.Set(squad, new Suppression { Value = 1f });
            RetreatPointAt(new float3(60f, 0f, 0f));

            World.Run(1f);

            Assert.IsTrue(IsRetreating(squad));
        }

        [Test]
        public void WornSquad_RetreatsUnlessItIsAlreadySafeAtThePoint()
        {
            var worn = Squad(new float3(0f, 0f, 30f), dead: 2);
            var safe = Squad(new float3(55f, 0f, -30f), dead: 2);
            var healthy = Squad(new float3(0f, 0f, -30f), dead: 1);
            RetreatPointAt(new float3(60f, 0f, -30f));

            World.Run(1f);

            Assert.IsTrue(IsRetreating(worn), "half its members left");
            Assert.IsFalse(IsRetreating(safe), "already inside the point's radius");
            Assert.IsFalse(IsRetreating(healthy), "three of four is enough to fight");
        }

        [Test]
        public void EasyBots_NeverRetreat_AndOthersDo()
        {
            var match = World.Own(new GameObject("Match")).AddComponent<MatchAuthoring>();
            var easy = match.AITuningFor(AIDifficulty.Easy).ToComponent(AIDifficulty.Easy);
            Assert.IsFalse(easy.RetreatsSquads);
            Assert.IsTrue(match.AITuningFor(AIDifficulty.Normal).ToComponent(AIDifficulty.Normal).RetreatsSquads);

            var squad = Squad(float3.zero);
            World.Set(squad, new Suppression { Value = 1f });
            RetreatPointAt(new float3(60f, 0f, 0f));
            easy.ThinkInterval = 0.5f;
            easy.TimeUntilThink = 0.5f;
            World.Set(World.Player(Bot), easy);

            World.Run(1f);

            Assert.IsFalse(IsRetreating(squad));
        }

        [Test]
        public void HurtSquadAtAReinforcePoint_Reinforces_AndSitsOutAttacks()
        {
            var ai = World.Get<AIPlayer>(World.Player(Bot));
            ai.AttackWaveSize = 1;
            World.Set(World.Player(Bot), ai);
            World.SpawnBuilding(1, new float3(80f, 0f, 0f), new float2(4f, 4f));
            var point = World.SpawnBuilding(Bot, new float3(10f, 0f, 0f), new float2(4f, 4f));
            World.Add(point, new ReinforcePoint { Range = 25f });
            World.AddResources(Bot, _supplies, 500);
            var squad = Squad(float3.zero, dead: 1);
            World.EntityManager.AddBuffer<ResourceCost>(squad);
            World.SetCost(squad, _supplies, 100);

            World.Run(1.2f);

            Assert.IsTrue(World.IsEnabled<Reinforcing>(squad));
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(squad), "a squad being reinforced is not sent to attack");
        }

        [Test]
        public void BuilderSquad_DigsAFieldWorkLineAtADefencePoint_OnceAndOnlyAfterTheBuildOrder()
        {
            var work = FieldWorkPrefab(FieldWorkRole.Cover);
            World.MakeBuilder(Squad(new float3(-10f, 0f, 0f)), work);
            World.AddResources(Bot, _supplies, 500);
            World.SpawnBuilding(Bot, float3.zero, new float2(4f, 4f), name: "HQ");
            DefencePointAt(new Vector2(20f, 0f));
            var unmet = World.MakePrefab(World.SpawnBuilding(0, float3.zero, new float2(4f, 4f), complete: false,
                name: "Depot"));
            World.SetCost(unmet, _supplies, 9999);
            var steps = World.EntityManager.GetBuffer<AIBuildStep>(World.Player(Bot));
            steps.Add(new AIBuildStep { Prefab = unmet, Count = 1 });

            World.Run(1.5f);
            Assert.AreEqual(0, World.Owned(work, Bot).Count, "the build order is not done");

            World.EntityManager.GetBuffer<AIBuildStep>(World.Player(Bot)).Clear();
            World.Run(3f);
            var first = World.Owned(work, Bot);
            Assert.GreaterOrEqual(first.Count, 2, "a line of segments");
            foreach (var segment in first)
            {
                Assert.AreEqual(20f, World.Get<LocalTransform>(segment).Position.x, 1f, "across the approach");
            }

            World.Run(10f);
            Assert.AreEqual(first.Count, World.Owned(work, Bot).Count, "no second line in the same place");
        }

        private Entity FieldWorkPrefab(FieldWorkRole role)
        {
            var work = World.MakePrefab(World.SpawnBuilding(0, float3.zero, new float2(3f, 1f), complete: false,
                buildTime: 0.5f, name: "FieldWork"));
            World.Add(work, new LinePlacement());
            World.Add(work, new AIFieldWork { Role = role });
            World.SetCost(work, _supplies, 5);
            return work;
        }

        private void DefencePointAt(Vector2 point)
        {
            var data = new AIMapHintsData();
            data.AddSlot(1, Array.Empty<Vector2>(), Array.Empty<Vector2>());
            data.AddSlot(Bot, Array.Empty<Vector2>(), new[] { point });
            data.AddLane(1, Bot, Array.Empty<Vector2>());
            data.AddLane(Bot, 1, Array.Empty<Vector2>());
            World.CreateHints(data);
            var ai = World.Get<AIPlayer>(World.Player(Bot));
            ai.FieldWorkLines = 1;
            World.Set(World.Player(Bot), ai);
        }
    }
}
