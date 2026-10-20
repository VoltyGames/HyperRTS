using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The breakable parts system on a hand-made building: the hit side breaks first.</summary>
    public class BreakablePartsSystemTests
    {
        private static readonly float3 Origin = new(10f, 0f, 10f);

        private World _world;
        private EntityManager _em;
        private BreakablePartsSystem _system;
        private Entity _cues;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Breakable Parts Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystemManaged<BreakablePartsSystem>();
            _cues = _em.CreateEntity(typeof(CueQueue), typeof(CueEvent));
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void FirstFrame_ShowsTheBreakageForTheHealthInBakeOrder()
        {
            var house = Building(0.5f, out var parts);

            _system.Update();

            Assert.AreEqual(2, _em.GetComponentData<ShownBreakage>(house).Broken);
            AssertBroken(parts[0], true);
            AssertBroken(parts[1], true);
            AssertBroken(parts[2], false);
            AssertBroken(parts[3], false);
        }

        [Test]
        public void Damage_BreaksThePartNearestTheImpact()
        {
            var house = Building(1f, out var parts);
            _system.Update();

            Impact(Origin + new float3(0f, 1f, -4.5f));
            SetHealth(house, 0.75f);
            _system.Update();

            AssertBroken(parts[3], true);
            AssertBroken(parts[0], false);
            AssertBroken(parts[1], false);
            AssertBroken(parts[2], false);
        }

        [Test]
        public void ImpactFarAway_DoesNotPickASide()
        {
            var house = Building(1f, out var parts);
            _system.Update();

            Impact(Origin + new float3(0f, 0f, -40f));
            SetHealth(house, 0.75f);
            _system.Update();

            Assert.IsFalse(_em.GetComponentData<ShownBreakage>(house).HasHit);
            AssertBroken(parts[0], true);
        }

        [Test]
        public void Repair_RestoresTheLatestBrokenFirst()
        {
            var house = Building(1f, out var parts);
            _system.Update();
            Impact(Origin + new float3(-4.5f, 1f, 0f));
            SetHealth(house, 0.75f);
            _system.Update();
            ClearImpacts();
            Impact(Origin + new float3(0f, 1f, 4.5f));
            SetHealth(house, 0.5f);
            _system.Update();
            AssertBroken(parts[1], true);
            AssertBroken(parts[2], true);

            SetHealth(house, 0.75f);
            _system.Update();

            AssertBroken(parts[1], true);
            AssertBroken(parts[2], false);
            Assert.AreEqual(1, _em.GetComponentData<ShownBreakage>(house).Broken);
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
            var sides = new[]
            {
                new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f), new float3(0f, 1f, 4f), new float3(0f, 1f, -4f),
            };
            parts = new BreakablePart[sides.Length];
            for (var i = 0; i < sides.Length; i++)
            {
                parts[i] = new BreakablePart
                {
                    Intact = Child(),
                    Broken = Child(),
                    Centre = sides[i],
                    Outward = math.normalize(new float3(sides[i].x, 0f, sides[i].z)),
                };
            }

            var buffer = _em.AddBuffer<BreakablePart>(house);
            foreach (var part in parts)
            {
                buffer.Add(part);
            }

            return house;
        }

        private Entity Child()
        {
            var child = _em.CreateEntity();
            _em.AddComponentData(child, LocalTransform.Identity);
            return child;
        }

        private void SetHealth(Entity house, float fraction) =>
            _em.SetComponentData(house, new Health { Current = 100f * fraction, Max = 100f });

        private void Impact(float3 at) =>
            _em.GetBuffer<CueEvent>(_cues).Add(new CueEvent { Slot = CueSlot.Impact, Position = at });

        private void ClearImpacts() => _em.GetBuffer<CueEvent>(_cues).Clear();

        private void AssertBroken(in BreakablePart part, bool broken)
        {
            Assert.AreEqual(broken ? 0f : 1f, _em.GetComponentData<LocalTransform>(part.Intact).Scale, "intact scale");
            Assert.AreEqual(broken ? 1f : 0f, _em.GetComponentData<LocalTransform>(part.Broken).Scale, "broken scale");
        }
    }
}
