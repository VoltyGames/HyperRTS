using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Surfaces;
using HyperRTS.Simulation.Squads.Cover;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// Heavy blasts on open ground dig crater cover; small blasts, other surfaces, spots nearby and other craters
    /// dig none, and past the cap the oldest crater goes.
    /// </summary>
    public class CraterTests : SquadFixture
    {
        private const float MinRadius = 3f;
        private const int MaxCount = 3;

        private SystemHandle _system;

        [SetUp]
        public void CreateCraterRules()
        {
            var prefab = World.EntityManager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
            World.EntityManager.AddBuffer<CoverSpot>(prefab).Add(new CoverSpot
            {
                Position = new float3(1f, 0f, 0f), Facing = new float2(-1f, 0f), Kind = CoverKind.Light,
            });
            var settings = CraterSettings.Standard;
            settings.minRadius = MinRadius;
            settings.maxCount = MaxCount;
            World.EntityManager.AddComponentData(World.EntityManager.CreateEntity(), settings.Rules(prefab));
            _system = World.World.GetExistingSystem<CraterSystem>();
        }

        private void Blast(float x, float radius = 5f, ImpactSurface surface = ImpactSurface.Ground) =>
            Blast(new CueEvent
            {
                Slot = CueSlot.Impact, Surface = surface, Radius = radius, Position = new float3(x, 0f, 0f),
            });

        private void Blast(params CueEvent[] impacts)
        {
            World.Tick();
            using var query = World.Query(typeof(CueQueue));
            var cues = World.EntityManager.GetBuffer<CueEvent>(query.GetSingletonEntity());
            foreach (var impact in impacts)
            {
                cues.Add(impact);
            }

            _system.Update(World.World.Unmanaged);
        }

        private NativeArray<LocalTransform> Craters()
        {
            using var query = World.Query(typeof(Crater), typeof(LocalTransform));
            return query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        }

        [Test]
        public void HeavyBlastOnOpenGround_DigsACraterThere()
        {
            Blast(20f);

            var craters = Craters();
            Assert.AreEqual(1, craters.Length);
            Assert.AreEqual(20f, craters[0].Position.x);
        }

        [Test]
        public void SmallArmsWaterAndStructures_DigNone()
        {
            Blast(new CueEvent { Slot = CueSlot.Impact, Surface = ImpactSurface.Ground, Radius = 0f });
            Blast(10f, MinRadius - 0.5f);
            Blast(20f, surface: ImpactSurface.Water);
            Blast(30f, surface: ImpactSurface.Structure);

            Assert.AreEqual(0, Craters().Length);
        }

        [Test]
        public void BlastNearACoverSpotOrAnotherCrater_DigsNone()
        {
            var prop = World.EntityManager.CreateEntity(typeof(LocalTransform));
            World.EntityManager.SetComponentData(prop, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));
            World.EntityManager.AddBuffer<CoverSpot>(prop).Add(new CoverSpot { Facing = new float2(0f, 1f) });

            Blast(51f);
            Assert.AreEqual(0, Craters().Length, "a prop's spot is 1 m away");

            Blast(20f);
            Blast(22f);
            Assert.AreEqual(1, Craters().Length, "the first crater is 2 m away");
        }

        [Test]
        public void PastTheCap_TheOldestCraterGoes()
        {
            for (var i = 0; i < MaxCount + 1; i++)
            {
                Blast(i * 20f);
            }

            var craters = Craters();
            Assert.AreEqual(MaxCount, craters.Length);
            foreach (var crater in craters)
            {
                Assert.Greater(crater.Position.x, 0f, "the one dug first is gone");
            }
        }
    }
}
