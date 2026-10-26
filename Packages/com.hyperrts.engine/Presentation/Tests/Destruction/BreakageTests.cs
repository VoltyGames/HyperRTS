using System;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>Staged wear: a hand-made building breaking on its hit side, and a resource pile shrinking as it empties.</summary>
    public class BreakageTests
    {
        private static readonly float3 Origin = new(10f, 0f, 10f);
        private static readonly float[] Thresholds = { 1f, 2f / 3f, 1f / 3f, 0.12f };

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private BreakablePartsSystem _parts;
        private SystemHandle _stages;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Breakage Tests");
            _em = _kit.EntityManager;
            _parts = _kit.World.CreateSystemManaged<BreakablePartsSystem>();
            _stages = _kit.World.CreateSystem<ResourceStageSystem>();
            _kit.AddCues();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void FirstFrame_ShowsTheBreakageForTheHealthInBakeOrder()
        {
            var house = Building(0.5f, out var parts);

            _parts.Update();

            Assert.AreEqual(2, _em.GetComponentData<ShownBreakage>(house).Broken);
            AssertBroken(parts, true, true, false, false);
        }

        [Test]
        public void Damage_BreaksThePartNearestTheImpact()
        {
            var house = Building(1f, out var parts);
            _parts.Update();

            Hit(house, Origin + new float3(0f, 1f, -4.5f), 0.75f);

            AssertBroken(parts, false, false, false, true);
        }

        [Test]
        public void ImpactFarAway_DoesNotPickASide()
        {
            var house = Building(1f, out var parts);
            _parts.Update();

            Hit(house, Origin + new float3(0f, 0f, -40f), 0.75f);

            Assert.IsFalse(_em.GetComponentData<ShownBreakage>(house).HasHit);
            AssertBroken(parts, true, false, false, false);
        }

        [Test]
        public void Repair_RestoresTheLatestBrokenFirst()
        {
            var house = Building(1f, out var parts);
            _parts.Update();
            Hit(house, Origin + new float3(-4.5f, 1f, 0f), 0.75f);
            _kit.ClearCues();
            Hit(house, Origin + new float3(0f, 1f, 4.5f), 0.5f);
            AssertBroken(parts, false, true, true, false);

            SetHealth(house, 0.75f);
            _parts.Update();

            AssertBroken(parts, false, true, false, false);
            Assert.AreEqual(1, _em.GetComponentData<ShownBreakage>(house).Broken);
        }

        [TestCase(201, 0)]
        [TestCase(200, 1)]
        [TestCase(100, 2)]
        [TestCase(1, 3)]
        public void Pile_FirstFrameShowsTheStageForTheRemainingShare(int amount, int expected)
        {
            var pile = Pile(amount, out var visuals);

            _stages.Update(_kit.World.Unmanaged);

            Assert.AreEqual(expected, _em.GetComponentData<ShownResourceStage>(pile).Index);
            AssertOnlyShown(visuals, expected);
        }

        [Test]
        public void Pile_GatheringStepsDown_AndRegrowthStepsBackUp()
        {
            var pile = Pile(300, out var visuals);
            _stages.Update(_kit.World.Unmanaged);

            SetAmount(pile, 90);
            _stages.Update(_kit.World.Unmanaged);
            AssertOnlyShown(visuals, 2);

            SetAmount(pile, 250);
            _stages.Update(_kit.World.Unmanaged);
            AssertOnlyShown(visuals, 0);
        }

        private Entity Building(float health, out BreakablePart[] parts)
        {
            var house = _em.CreateEntity();
            _em.AddComponentData(house, new LocalToWorld { Value = float4x4.Translate(Origin) });
            _em.AddComponentData(house, new Health { Current = 100f * health, Max = 100f });
            _em.AddComponentData(house, new BreakableParts
            {
                BreakShare = 1f,
                BoundsCentre = new float3(0f, 1f, 0f),
                BoundsExtents = new float3(4.5f, 1.5f, 4.5f),
            });
            _em.AddComponentData(house, new ShownBreakage());
            _em.AddBuffer<Child>(house);
            var sides = new[]
            {
                new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f), new float3(0f, 1f, 4f), new float3(0f, 1f, -4f),
            };
            parts = new BreakablePart[sides.Length];
            for (var i = 0; i < sides.Length; i++)
            {
                parts[i] = new BreakablePart
                {
                    Intact = Piece(house),
                    Broken = Piece(house),
                    Centre = sides[i],
                    Outward = math.normalize(new float3(sides[i].x, 0f, sides[i].z)),
                };
            }

            _em.AddBuffer<BreakablePart>(house).AddRange(new NativeArray<BreakablePart>(parts, Allocator.Temp));
            return house;
        }

        // A piece mesh under the house, as a SubScene building has it.
        private Entity Piece(Entity house)
        {
            var piece = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(piece, new Parent { Value = house });
            _em.GetBuffer<Child>(house).Add(new Child { Value = piece });
            return piece;
        }

        private void Hit(Entity house, float3 at, float health)
        {
            _kit.PushCue(new CueEvent { Slot = CueSlot.Impact, Position = at });
            SetHealth(house, health);
            _parts.Update();
        }

        private void SetHealth(Entity house, float fraction) =>
            _em.SetComponentData(house, new Health { Current = 100f * fraction, Max = 100f });

        private void AssertBroken(BreakablePart[] parts, params bool[] broken)
        {
            for (var i = 0; i < parts.Length; i++)
            {
                Assert.AreEqual(!broken[i], _em.IsComponentEnabled<MaterialMeshInfo>(parts[i].Intact), $"part {i} intact drawn");
                Assert.AreEqual(broken[i], _em.IsComponentEnabled<MaterialMeshInfo>(parts[i].Broken), $"part {i} broken drawn");
            }
        }

        // A pile whose stage models each hold a child mesh, linked as an instantiated prefab is.
        private Entity Pile(int amount, out Entity[] visuals)
        {
            var pile = _em.CreateEntity(typeof(HiddenBy));
            _em.AddComponentData(pile, new ResourceNode { Amount = amount, MaxAmount = 300 });
            _em.AddComponentData(pile, new ShownResourceStage { Index = -1 });
            var stages = new ResourceStage[Thresholds.Length];
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { pile };
            visuals = new Entity[Thresholds.Length];
            for (var i = 0; i < Thresholds.Length; i++)
            {
                visuals[i] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                var mesh = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                _em.AddComponentData(mesh, new Parent { Value = visuals[i] });
                stages[i] = new ResourceStage { Below = Thresholds[i], Visual = visuals[i] };
                group.Add(visuals[i]);
                group.Add(mesh);
            }

            _em.AddBuffer<ResourceStage>(pile).AddRange(new NativeArray<ResourceStage>(stages, Allocator.Temp));
            _em.AddBuffer<LinkedEntityGroup>(pile).AddRange(group.AsArray());
            return pile;
        }

        private void SetAmount(Entity pile, int amount)
        {
            var node = _em.GetComponentData<ResourceNode>(pile);
            node.Amount = amount;
            _em.SetComponentData(pile, node);
        }

        private void AssertOnlyShown(Entity[] visuals, int shown)
        {
            for (var i = 0; i < visuals.Length; i++)
            {
                Assert.AreEqual(i == shown, _em.IsComponentEnabled<MaterialMeshInfo>(visuals[i]), $"stage {i}");
            }

            using var query = _em.CreateEntityQuery(typeof(Parent));
            using var meshes = query.ToEntityArray(Allocator.Temp);
            foreach (var mesh in meshes)
            {
                var stage = Array.IndexOf(visuals, _em.GetComponentData<Parent>(mesh).Value);
                Assert.AreEqual(stage == shown, _em.IsComponentEnabled<MaterialMeshInfo>(mesh), $"stage {stage} child");
            }
        }
    }
}
