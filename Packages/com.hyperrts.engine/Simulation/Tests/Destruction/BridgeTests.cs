using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Destruction
{
    /// <summary>
    /// A repairable bridge: its spans carry the deck, so a span that falls closes the crossing, and engineers rebuild
    /// the spans one at a time, even when every span is down.
    /// </summary>
    public class BridgeTests
    {
        private const int SpanCount = 4;
        private const float SpanLength = 5f;
        private const float SectionTime = 10f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private static float3 Span(int index) => new(0f, 0f, (index - (SpanCount - 1) * 0.5f) * SpanLength);

        /// <summary>
        /// Four deck spans over a river, 9 m wide, the end ones on the banks and each middle one resting on its end.
        /// </summary>
        private Entity Bridge()
        {
            _world.SpawnNavArea(float3.zero, new float2(60f, SpanCount * SpanLength), NavAreaKind.Water);
            var sections = new List<Section>();
            for (var i = 0; i < SpanCount; i++)
            {
                var onBank = i == 0 || i == SpanCount - 1;
                var rests = i == 1 ? 1ul : 0ul;
                if (i == 2)
                {
                    rests = 1ul << (SpanCount - 1);
                }

                sections.Add(new Section
                {
                    Center = Span(i), HalfSize = new float3(4.5f, 0.3f, SpanLength * 0.5f), Health = 100f,
                    MaxHealth = 100f, Anchored = onBank, Deck = true, Supports = rests, Outward = math.up(),
                });
            }

            var bridge = _world.SpawnBuilding(0, float3.zero, new float2(9f, SpanCount * SpanLength));
            var writer = new EntityWriter(_world.EntityManager, bridge);
            NavSetup.AddArea(writer, new float2(9f, SpanCount * SpanLength), NavAreaKind.Deck);
            SectionsSetup.Add(writer, sections, new List<SectionFactor>(), Entity.Null);
            SectionsSetup.AddRepairable(writer, SectionTime);
            _world.EntityManager.SetComponentData(bridge, new Health { Current = 100f * SpanCount, Max = 100f * SpanCount });
            return bridge;
        }

        private void Hit(Entity bridge, int span) => _world.QueueDamage(new DamageEvent
        {
            Target = bridge, Origin = Span(span) + new float3(0f, 20f, 0f), Position = Span(span), SourceFaction = 2,
            Amount = 100f,
        });

        private bool Open(int span) => _world.Grid().IsWalkable(Span(span));

        [Test]
        public void SpanThatFalls_ClosesTheCrossingUnderIt_AndTheOthersStayOpen()
        {
            var bridge = Bridge();
            _world.Tick(frames: 2);
            Assert.IsTrue(Open(1), "the deck carries units over the river");

            Hit(bridge, 1);
            _world.Tick(frames: 3);

            Assert.IsFalse(Open(1), "the river is back under the fallen span");
            Assert.IsFalse(_world.Grid().IsWalkable(Span(1) + new float3(4f, 0f, 0f)), "across its whole width");
            Assert.IsTrue(Open(0) && Open(2) && Open(3), "the other spans still carry units");
        }

        [Test]
        public void Engineers_RebuildEverySpanOneByOne_FromAWreck()
        {
            var bridge = Bridge();
            _world.Tick(frames: 2);
            for (var i = 0; i < SpanCount; i++)
            {
                Hit(bridge, i);
            }

            _world.Tick(frames: 3);
            Assert.IsTrue(_world.EntityManager.Exists(bridge), "a bridge with every span down is not destroyed");
            Assert.AreEqual(Repairable.WreckHealth, _world.GetHealth(bridge), 1e-3f);
            Assert.IsFalse(Open(1) || Open(2));

            var engineer = _world.MakeBuilder(_world.SpawnUnit(1, new float3(0f, 0f, -14f)));
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = engineer, Target = bridge });
            _world.RunUntil(() => Open(0) || Open(3), 20f);
            Assert.IsFalse(Open(1) || Open(2), "only the banks' spans come back first, since the middle ones need them");

            _world.RunUntil(() => Open(0) && Open(3), 40f);
            _world.RunUntil(() => Open(1) && Open(2), 40f);

            Assert.IsTrue(Open(0) && Open(1) && Open(2) && Open(3), "the crossing reopens");
            Assert.AreEqual(100f * SpanCount, _world.GetHealth(bridge), 1e-3f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(engineer), "the order ends when it is whole");
        }

        [Test]
        public void SpanNeedingEverySupport_FallsWhenOneSupportGoes()
        {
            var bridge = Bridge();
            var sections = _world.EntityManager.GetBuffer<Section>(bridge);
            var span = sections[1];
            span.Supports = 1ul | (1ul << (SpanCount - 1));
            span.NeedsAllSupports = true;
            sections[1] = span;
            _world.Tick(frames: 2);
            Assert.IsTrue(Open(1));

            Hit(bridge, 0);
            _world.RunUntil(() => !Open(1), 5f);

            Assert.IsFalse(Open(1), "one pier gone brings the span down");
            Assert.IsTrue(Open(3), "the span over the other bank stays");
        }
    }
}
