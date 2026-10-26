using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Simulation.Tests.Environment
{
    /// <summary>Ground material maps, the effect rig each material picks, motion dust and entity loop sounds.</summary>
    public class SurfaceTests
    {
        private static readonly MotionDust Dust = new() { MinSpeed = 1.5f, FullSpeed = 8f };

        private GroundMaterials _materials;

        // A 3 x 2 map of 2 m cells from (10, 20): south row Soil, Grass, Mud; north row Sand, Gravel, Water.
        [SetUp]
        public void SetUp()
        {
            var cells = new NativeArray<byte>(new[]
            {
                (byte)SurfaceMaterial.Soil, (byte)SurfaceMaterial.Grass, (byte)SurfaceMaterial.Mud,
                (byte)SurfaceMaterial.Sand, (byte)SurfaceMaterial.Gravel, (byte)SurfaceMaterial.Water,
            }, Allocator.Temp);
            _materials = GroundMaterials.Create(cells, new int2(3, 2), new float2(10f, 20f), 2f, Allocator.Persistent);
            cells.Dispose();
        }

        [TearDown]
        public void TearDown() => _materials.Blob.Dispose();

        [TestCase(10.1f, 20.1f, SurfaceMaterial.Soil)]
        [TestCase(13f, 21f, SurfaceMaterial.Grass)]
        [TestCase(15.9f, 23.9f, SurfaceMaterial.Water)]
        [TestCase(12f, 22f, SurfaceMaterial.Gravel)]
        public void At_ReturnsTheCellUnderThePoint(float x, float z, SurfaceMaterial expected) =>
            Assert.AreEqual(expected, _materials.At(new float2(x, z)));

        [TestCase(9.9f, 21f)]
        [TestCase(16f, 21f)]
        [TestCase(12f, 19.9f)]
        [TestCase(12f, 24f)]
        public void At_IsNoneOffTheMap(float x, float z) =>
            Assert.AreEqual(SurfaceMaterial.None, _materials.At(new float2(x, z)));

        [Test]
        public void MapAsset_BakesItsCellsAtTheCorner()
        {
            var map = Map(2, 1, (byte)SurfaceMaterial.Mud, (byte)SurfaceMaterial.Paved);
            var baked = map.Bake(new float2(-4f, 0f), Allocator.Persistent);

            Assert.AreEqual(SurfaceMaterial.Mud, baked.At(new float2(-3f, 1f)));
            Assert.AreEqual(SurfaceMaterial.Paved, baked.At(new float2(-1f, 1f)));
            Assert.AreEqual(SurfaceMaterial.None, baked.At(new float2(0.5f, 1f)));
            baked.Blob.Dispose();
            Object.DestroyImmediate(map);
        }

        [Test]
        public void MapAsset_WithTheWrongCellCount_IsAnError()
        {
            var map = Map(3, 2, (byte)SurfaceMaterial.Soil);
            var error = Assert.Throws<InvalidOperationException>(() => map.Bake(float2.zero, Allocator.Persistent));
            StringAssert.Contains("holds 1 cells, not 3 x 2", error.Message);
            Object.DestroyImmediate(map);
        }

        private static GroundMaterialMap Map(int width, int height, params byte[] cells)
        {
            var map = ScriptableObject.CreateInstance<GroundMaterialMap>();
            (map.width, map.height, map.spacing, map.cells) = (width, height, 2f, cells);
            return map;
        }

        [Test]
        public void RigFor_PicksTheMaterialsOwnRig_ThenTheCatchAll()
        {
            var effects = ScriptableObject.CreateInstance<SurfaceEffects>();
            var splash = new GameObject("Splash");
            var dust = new GameObject("Dust");
            effects.materials = new[] { new SurfaceEffects.Entry { material = SurfaceMaterial.Water, rig = splash } };

            Assert.IsNull(effects.RigFor(SurfaceMaterial.Soil), "no entry and no catch-all");
            effects.anyMaterial = dust;
            Assert.AreSame(splash, effects.RigFor(SurfaceMaterial.Water), "its own entry");
            Assert.AreSame(dust, effects.RigFor(SurfaceMaterial.Soil), "the catch-all");

            Object.DestroyImmediate(effects);
            Object.DestroyImmediate(splash);
            Object.DestroyImmediate(dust);
        }

        [Test]
        public void Dust_GrowsWithSpeed_FromNoneWhileCreeping_ToFullAtFullSpeed_EitherWay()
        {
            Assert.AreEqual(0f, Dust.Strength(0f));
            Assert.AreEqual(0f, Dust.Strength(Dust.MinSpeed), "creeping");
            Assert.Greater(Dust.Strength(5f), Dust.Strength(3f));
            Assert.AreEqual(1f, Dust.Strength(8f), 1e-5f);
            Assert.AreEqual(1f, Dust.Strength(12f), 1e-5f);
            Assert.AreEqual(Dust.Strength(4f), Dust.Strength(-4f), 1e-5f, "reversing raises dust too");
        }

        [Test]
        public void DustSpeed_IsSignedAlongTheFacing_AndIgnoresSidewaysPushes()
        {
            var dust = Dust;
            var forward = new float3(0f, 0f, 1f);

            Assert.AreEqual(0f, dust.DrawnSpeed(float3.zero, forward, 0.1f), "the first frame has nothing to compare");
            Assert.AreEqual(6f, dust.DrawnSpeed(new float3(0f, 0f, 0.6f), forward, 0.1f), 1e-4f);
            Assert.AreEqual(-3f, dust.DrawnSpeed(new float3(0f, 0f, 0.3f), forward, 0.1f), 1e-4f, "reversing");
            Assert.AreEqual(0f, dust.DrawnSpeed(new float3(0.5f, 0f, 0.3f), forward, 0.1f), 1e-4f, "pushed sideways");
            Assert.AreEqual(0f, dust.DrawnSpeed(new float3(9f, 0f, 9f), forward, 0f), "paused");
        }

        [Test]
        public void EntityLoops_OnlySetCuesGetALoop_WithTheTuning()
        {
            using var world = new World("Loops");
            var entity = world.EntityManager.CreateEntity();
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            var writer = new EntityManagerWriter(world.EntityManager, entity);

            var loops = EntityLoopsAuthoring.Add(ref writer, 8f, 0.15f);
            EntityLoopsAuthoring.Set(loops, LoopSlot.EngineIdle, cue);
            EntityLoopsAuthoring.Set(loops, LoopSlot.Rotor, null);
            EntityLoopsAuthoring.Set(loops, LoopSlot.Ambient, cue);

            loops = world.EntityManager.GetBuffer<EntityLoopSound>(entity);
            Assert.AreEqual(2, loops.Length);
            Assert.AreEqual(LoopSlot.EngineIdle, loops[0].Slot);
            Assert.AreEqual(LoopSlot.Ambient, loops[1].Slot);
            Assert.AreEqual(0.15f, world.EntityManager.GetComponentData<LoopSoundTuning>(entity).PitchRise);
            Object.DestroyImmediate(cue);
        }
    }
}
