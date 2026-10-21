using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The resource stage system on a four-stage pile: it shrinks as it empties and grows back as it regrows.</summary>
    public class ResourceStageSystemTests
    {
        private static readonly float[] Thresholds = { 1f, 2f / 3f, 1f / 3f, 0.12f };

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private ResourceStageSystem _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Resource Stage Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.CreateSystemManaged<ResourceStageSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [TestCase(300, 0)]
        [TestCase(201, 0)]
        [TestCase(200, 1)]
        [TestCase(100, 2)]
        [TestCase(36, 3)]
        [TestCase(1, 3)]
        public void FirstFrame_ShowsTheStageForTheRemainingShare(int amount, int expected)
        {
            var pile = Pile(amount, out var visuals);

            _system.Update();

            Assert.AreEqual(expected, _em.GetComponentData<ShownResourceStage>(pile).Index);
            AssertOnlyShown(visuals, expected);
        }

        [Test]
        public void Gathering_StepsDown_AndRegrowth_StepsBackUp()
        {
            var pile = Pile(300, out var visuals);
            _system.Update();

            SetAmount(pile, 90);
            _system.Update();
            AssertOnlyShown(visuals, 2);

            SetAmount(pile, 250);
            _system.Update();
            AssertOnlyShown(visuals, 0);
        }

        private Entity Pile(int amount, out Entity[] visuals)
        {
            var pile = _em.CreateEntity();
            _em.AddComponentData(pile, new ResourceNode { Amount = amount, MaxAmount = 300 });
            _em.AddComponentData(pile, new ShownResourceStage { Index = -1 });
            _em.AddBuffer<ResourceStage>(pile);
            visuals = new Entity[Thresholds.Length];
            for (var i = 0; i < Thresholds.Length; i++)
            {
                visuals[i] = _em.CreateEntity();
                _em.AddComponentData(visuals[i], LocalTransform.Identity);
                // Creating the visual moves chunks, so the buffer is fetched again.
                _em.GetBuffer<ResourceStage>(pile).Add(new ResourceStage { Below = Thresholds[i], Visual = visuals[i] });
            }

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
                Assert.AreEqual(i == shown ? 1f : 0f, _em.GetComponentData<LocalTransform>(visuals[i]).Scale, $"stage {i}");
            }
        }
    }
}
