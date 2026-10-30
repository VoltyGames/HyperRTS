using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>A crater stamped by the real brush, dented into a real terrain heightmap and put back.</summary>
    public class CraterTerrainTests
    {
        private const float Ground = 10f;
        private const float CraterDepth = 1f;
        private const float CraterDiameter = 8f;

        // One metre a heightmap texel from the corner at (-32, -32): off centre, so a flipped row shows.
        private static readonly float2 Crater = new(8f, -8f);
        private const int CraterX = 40;
        private const int CraterZ = 24;

        private TerrainData _data;
        private Terrain _terrain;
        private InteractionSurfaces _surfaces;
        private InteractionBrush _brush;
        private ImpactMark _mark;
        private CraterTerrain _crater;

        [SetUp]
        public void SetUp()
        {
            _data = new TerrainData { heightmapResolution = 65, size = new Vector3(64f, 20f, 64f) };
            var flat = new float[65, 65];
            for (var z = 0; z < 65; z++)
            {
                for (var x = 0; x < 65; x++)
                {
                    flat[z, x] = Ground / 20f;
                }
            }

            _data.SetHeights(0, 0, flat);
            _terrain = Terrain.CreateTerrainGameObject(_data).GetComponent<Terrain>();
            _terrain.transform.position = new Vector3(-32f, 0f, -32f);
            _surfaces = new InteractionSurfaces(new float2(-32f), new float2(64f), InteractionQuality.Full);
            _surfaces.Publish();
            _brush = new InteractionBrush(Shader.Find(InteractionBrush.ShaderName));
            _brush.Clear(_surfaces);
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _mark.craterDepth = CraterDepth;
            _crater = new CraterTerrain(_terrain, Resources.Load<Shader>(CraterTerrain.ShaderPath));
        }

        [TearDown]
        public void TearDown()
        {
            _crater.Dispose();
            _brush.Dispose();
            _surfaces.Dispose();
            InteractionSurfaces.Clear();
            Object.DestroyImmediate(_mark);
            Object.DestroyImmediate(_terrain.gameObject);
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Dent_FollowsTheStampedBowl()
        {
            StampCrater();

            Assert.AreEqual(CraterDepth, Sunk(CraterX, CraterZ), 0.05f, "the bowl's floor is not the stamped depth");
            Assert.That(Sunk(CraterX + 2, CraterZ), Is.InRange(0.35f, 0.75f), "the bowl wall is off");
            Assert.AreEqual(0f, Sunk(CraterX + 6, CraterZ), 1e-3f, "the ground past the rim sank");
            Assert.AreEqual(0f, Sunk(CraterX, 64 - CraterZ), 1e-3f, "the bowl shows up mirrored across the map");
        }

        [Test]
        public void Restore_PutsTheOriginalHeightsBack()
        {
            var before = _data.GetHeights(0, 0, 65, 65);
            StampCrater();

            _crater.Restore();

            Assert.AreEqual(before, _data.GetHeights(0, 0, 65, 65));
            Assert.IsFalse(_crater.Unsynced);
        }

        // Sinking shaders fade by the height above this ground, so it must not dent with the terrain.
        [Test]
        public void UndentedHeights_KeepTheGroundFromBeforeTheCrater()
        {
            var before = Texels(_data.heightmapTexture);
            StampCrater();

            var undented = Shader.GetGlobalTexture(CraterTerrain.UndentedHeightsId);
            var dented = Texels(_data.heightmapTexture);

            Assert.AreEqual(before, Texels(undented));
            Assert.AreNotEqual(before[CraterZ * 65 + CraterX], dented[CraterZ * 65 + CraterX], "the terrain did not dent");
            _crater.Dispose();
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(CraterTerrain.UndentedScaleId), "the heights outlived it");
            _crater = new CraterTerrain(_terrain, Resources.Load<Shader>(CraterTerrain.ShaderPath));
        }

        private static ushort[] Texels(Texture heights)
        {
            var request = AsyncGPUReadback.Request(heights, 0);
            request.WaitForCompletion();
            return request.GetData<ushort>().ToArray();
        }

        private void StampCrater()
        {
            var stamps = new InteractionStamps();
            stamps.AddCrater(Crater, _mark, CraterDiameter);
            _brush.Draw(_surfaces, 0, 0, 0, stamps);
            _crater.Dent(Crater - CraterDiameter, Crater + CraterDiameter);
            _crater.Sync();
        }

        private float Sunk(int x, int z) => Ground - _data.GetHeight(x, z);
    }
}
