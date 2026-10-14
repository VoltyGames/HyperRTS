using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Impact events for impact marks: who writes them, where they land and what surface they struck.</summary>
    public class ImpactTests
    {
        private TestWorld _world;
        private ImpactMark _mark;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_mark);
        }

        internal static Entity Marked(TestWorld world, Entity shooter, ImpactMark mark)
        {
            var writer = new EntityManagerWriter(world.EntityManager, shooter);
            ImpactMarkSetup.Add(ref writer, new[] { mark });
            return shooter;
        }

        internal static ImpactEvent[] Queued(TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(ImpactQueue));
            return world.EntityManager.GetBuffer<ImpactEvent>(query.GetSingletonEntity()).AsNativeArray().ToArray();
        }

        /// <summary>Every impact of the next frames; the queue itself only holds the last frame's.</summary>
        private List<ImpactEvent> Landed(float seconds)
        {
            var landed = new List<ImpactEvent>();
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                landed.AddRange(Queued(_world));
            }

            return landed;
        }

        private Entity Gun(float3 position, float range = 6f, WeaponTargets targets = WeaponTargets.Surface) =>
            Marked(_world, _world.AddWeapon(_world.SpawnUnit(1, position, name: "Mortar"), range: range,
                cooldown: 100f, targets: targets), _mark);

        private int GetTypeId(Entity entity) => _world.Get<EntityInfo>(entity).TypeId;

        [Test]
        public void InstantHit_LandsOnceOnTheGroundAtItsTarget_UnderTheShootersType()
        {
            var gun = Gun(float3.zero);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var landed = Landed(1f);

            Assert.AreEqual(1, landed.Count, "one shot, reported for its frame only");
            Assert.AreEqual(GetTypeId(gun), landed[0].TypeId);
            Assert.AreEqual(new float3(3f, 0f, 0f), landed[0].Position);
            Assert.AreEqual(1, landed[0].Faction);
            Assert.AreEqual(ImpactSurface.Ground, landed[0].Surface);
        }

        [Test]
        public void ShotsWithoutMarks_WriteNoImpacts()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            Assert.IsEmpty(Landed(2f));
        }

        [Test]
        public void ProjectileImpact_LandsOnArrival_EvenAfterTheShooterDied()
        {
            var shell = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            _world.EntityManager.SetComponentData(shell, LocalTransform.Identity);
            var gun = Marked(_world, _world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Howitzer"), range: 8f,
                cooldown: 100f, projectile: shell), _mark);
            var typeId = GetTypeId(gun);
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            for (var frame = 0; frame < 30 && projectiles.IsEmpty; frame++)
            {
                _world.Tick();
            }

            _world.EntityManager.DestroyEntity(gun);
            var landed = Landed(2f);

            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(typeId, landed[0].TypeId);
            Assert.AreEqual(6f, landed[0].Position.x, 0.5f);
            Assert.AreEqual(ImpactSurface.Ground, landed[0].Surface, "the target is gone or not; the ground is land");
        }

        [Test]
        public void HitOnABuilding_StruckAStructure()
        {
            Gun(float3.zero);
            _world.SpawnBuilding(2, new float3(4f, 0f, 0f), new float2(2f, 2f));

            Assert.AreEqual(ImpactSurface.Structure, Landed(1f)[0].Surface);
        }

        [Test]
        public void HitOnAnAircraft_StruckTheAir()
        {
            Gun(float3.zero, targets: WeaponTargets.Air);
            _world.SpawnAircraft(2, new float3(3f, 0f, 0f));

            Assert.AreEqual(ImpactSurface.Air, Landed(1f)[0].Surface);
        }

        [Test]
        public void HitOnAShip_StruckTheWater()
        {
            _world.ConfigureMap(waterLevel: -1f);
            _world.SpawnNavArea(new float3(10f, 0f, 0f), new float2(10f, 40f), NavAreaKind.Water);
            Gun(float3.zero, range: 10f);
            _world.SpawnShip(2, new float3(9f, 0f, 0f));

            var landed = Landed(1f)[0];
            Assert.AreEqual(ImpactSurface.Water, landed.Surface);
            Assert.AreEqual(SurfaceMaterial.Water, landed.Material, "a ship without a hit material splashes");
        }

        [Test]
        public void HitOnATargetWithAHitMaterial_StruckThatMaterial_OpenGroundTheGroundMap()
        {
            var cells = new NativeArray<byte>(64 * 64, Allocator.Temp);
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = (byte)SurfaceMaterial.Sand;
            }

            var ground = GroundMaterials.Create(cells, new int2(64, 64), new float2(-32f, -32f), 2f, Allocator.Persistent);
            cells.Dispose();
            _world.EntityManager.CreateSingleton(ground);
            Gun(float3.zero);
            Gun(new float3(0f, 0f, 40f));
            var tank = _world.SpawnUnit(2, new float3(3f, 0f, 0f));
            var writer = new EntityManagerWriter(_world.EntityManager, tank);
            EffectSetup.AddHitMaterial(ref writer, SurfaceMaterial.Metal);
            _world.SpawnUnit(2, new float3(3f, 0f, 40f));

            var landed = Landed(1f);

            Assert.AreEqual(2, landed.Count);
            var onTank = landed[0].Position.z < 20f ? landed[0] : landed[1];
            var onGround = landed[0].Position.z < 20f ? landed[1] : landed[0];
            Assert.AreEqual(SurfaceMaterial.Metal, onTank.Material);
            Assert.AreEqual(SurfaceMaterial.Sand, onGround.Material, "open ground reads the ground map");
            ground.Blob.Dispose();
        }

        [Test]
        public void ShotsWithOnlyImpactEffects_StillWriteImpacts()
        {
            var effects = ScriptableObject.CreateInstance<SurfaceEffects>();
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero, name: "Rifle"), cooldown: 100f);
            var writer = new EntityManagerWriter(_world.EntityManager, gun);
            EffectSetup.AddImpactEffects(ref writer, effects);
            _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            var landed = Landed(1f);
            Object.DestroyImmediate(effects);

            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(GetTypeId(gun), landed[0].TypeId);
        }

        [Test]
        public void HitOnABridge_StruckAStructure_AndInAFord_TheWater()
        {
            _world.ConfigureMap(waterLevel: -1f);
            _world.SpawnNavArea(float3.zero, new float2(6f, 200f), NavAreaKind.Water);
            _world.SpawnNavArea(new float3(0f, 2f, 0f), new float2(10f, 4f), NavAreaKind.Deck);
            _world.SpawnNavArea(new float3(0f, -1.15f, 40f), new float2(10f, 4f), NavAreaKind.Deck);
            Gun(new float3(-8f, 0f, 0f), range: 10f);
            Gun(new float3(-8f, 0f, 40f), range: 10f);
            _world.SpawnUnit(2, new float3(0f, 2f, 0f));
            _world.SpawnUnit(2, new float3(0f, -1.15f, 40f));

            var landed = Landed(1f);

            Assert.AreEqual(2, landed.Count);
            var onBridge = landed[0].Position.z < 20f ? landed[0] : landed[1];
            var inFord = landed[0].Position.z < 20f ? landed[1] : landed[0];
            Assert.AreEqual(ImpactSurface.Structure, onBridge.Surface);
            Assert.AreEqual(ImpactSurface.Water, inFord.Surface);
        }
    }
}
