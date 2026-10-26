using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Fog
{
    /// <summary>Fog on screen: hiding fogged roots and passengers with every part, the fog texels and hierarchy collection.</summary>
    public class FogTests
    {
        private const int Parts = 100;

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _fog;
        private EntityCommandBufferSystem _commands;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Fog Tests");
            _em = _kit.EntityManager;
            _commands = _kit.World.GetOrCreateSystemManaged<BeginPresentationEntityCommandBufferSystem>();
            _fog = _kit.World.CreateSystem<FogVisibilitySystem>();
            _kit.AddPlayer(1, local: true);
            _kit.AddPlayer(2);
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void FoggedRoot_StopsRendering_UntilRevealed()
        {
            var enemy = Spawn(2);
            var child = Part(enemy);
            var own = Spawn(1);
            _em.AddComponent<FogHidden>(enemy);

            Run();
            Assert.IsFalse(Drawn(enemy));
            Assert.IsFalse(Drawn(child));
            Assert.IsTrue(Drawn(own));

            _em.RemoveComponent<FogHidden>(enemy);
            Run();
            Assert.IsTrue(Drawn(enemy));
            Assert.IsTrue(Drawn(child));
        }

        [Test]
        public void PassengerInside_IsHidden_AndShownAgainOnceOut()
        {
            var passenger = Spawn(1);
            var child = Part(passenger);
            _em.AddComponentData(passenger, new Inside { Container = Entity.Null });

            Run();
            Assert.IsFalse(Drawn(child), "aboard: not drawn");

            _em.SetComponentEnabled<Inside>(passenger, false);
            Run();
            Assert.IsTrue(Drawn(passenger));
            Assert.IsTrue(Drawn(child), "out again: drawn");
        }

        [Test]
        public void Reveal_KeepsAPartHiddenForAnotherReason()
        {
            var enemy = Spawn(2);
            var stage = Part(enemy);
            _em.SetComponentData(stage, new HiddenBy { Reasons = HideReason.Stage });
            _em.SetComponentEnabled<MaterialMeshInfo>(stage, false);
            _em.AddComponent<FogHidden>(enemy);
            Run();

            _em.RemoveComponent<FogHidden>(enemy);
            Run();
            Assert.IsTrue(Drawn(enemy));
            Assert.IsFalse(Drawn(stage), "still not the stage on screen");
        }

        [Test]
        public void Texels_ClearWhenVisible_DimWhenExplored_DarkOtherwise()
        {
            const byte team = 2;
            var mask = (ushort)(1 << team);
            using var visible = new NativeArray<ushort>(new ushort[] { mask, 0, 0, 1 << 1 }, Allocator.TempJob);
            using var explored = new NativeArray<ushort>(new ushort[] { mask, mask, 0, 1 << 1 }, Allocator.TempJob);
            using var output = new NativeArray<byte>(4, Allocator.TempJob);

            FogTexels.Fill(visible, explored, team, 128, 217, output);

            CollectionAssert.AreEqual(new byte[] { 0, 128, 217, 217 }, output.ToArray());
        }

        [Test]
        public void Collect_LinkedGroupPastSixtyThreeParts_ListsEveryPart()
        {
            var root = _em.CreateEntity();
            _em.AddBuffer<LinkedEntityGroup>(root).Add(root);
            for (var i = 1; i < Parts; i++)
            {
                var part = _em.CreateEntity();
                _em.GetBuffer<LinkedEntityGroup>(root).Add(part);
            }

            Assert.AreEqual(Parts, CollectedCount(root));
        }

        [Test]
        public void Collect_TransformChildrenPastSixtyThreeParts_ListsEveryPart()
        {
            var root = _em.CreateEntity();
            _em.AddBuffer<Child>(root);
            for (var i = 1; i < Parts; i++)
            {
                var part = _em.CreateEntity();
                _em.GetBuffer<Child>(root).Add(new Child { Value = part });
            }

            Assert.AreEqual(Parts, CollectedCount(root));
        }

        // The fog visibility system collects hierarchies itself, so its state builds the lookups.
        private int CollectedCount(Entity root)
        {
            ref var state = ref _kit.World.Unmanaged.ResolveSystemStateRef(_fog);
            using var targets = new NativeList<Entity>(Allocator.Temp);
            RenderHierarchy.Collect(root, state.GetBufferLookup<LinkedEntityGroup>(true),
                state.GetBufferLookup<Child>(true), targets);
            return targets.Length;
        }

        // A mesh in the root's group, as baking leaves it.
        private Entity Part(Entity root)
        {
            var part = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { root, part }, Allocator.Temp));
            return part;
        }

        private bool Drawn(Entity entity) => _em.IsComponentEnabled<MaterialMeshInfo>(entity);

        private Entity Spawn(byte faction)
        {
            var entity = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(EntityInfo), typeof(HiddenBy));
            _em.AddComponentData(entity, new Faction { Value = faction });
            _em.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            return entity;
        }

        private void Run()
        {
            _fog.Update(_kit.World.Unmanaged);
            _commands.Update();
            _em.CompleteAllTrackedJobs();
        }
    }
}
