using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Effects;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Effects.Streams;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>
    /// A recipe with one layer of every kind, every optional field set, a streamed strip and a child of a source with
    /// random frames builds a graph the pool can play whose shaders compile, and the stamp finds stale graphs.
    /// </summary>
    public class EffectGraphBuilderTests
    {
        private const string Folder = "Assets/_EffectGraphBuilderTests";
        private const string Recipe = Folder + "/AllKinds" + EffectRecipe.Extension;
        private const string Graph = Folder + "/AllKinds.vfx";
        private const string Chunk = Folder + "/Chunk.asset";
        private const string Texture = "Packages/com.hyperrts.engine/Presentation/Environment/EdgeNoise.png";
        private const string TestBurst = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst" + EffectRecipe.Extension;
        private const string TestStream = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestStream" + EffectRecipe.Extension;

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void EveryLayerKind_BuildsAGraphWithItsEventsAndProperties()
        {
            AssetDatabase.CreateFolder("Assets", "_EffectGraphBuilderTests");
            AssetDatabase.CreateAsset(Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx")), Chunk);
            AssetDatabase.CreateAsset(AllKinds(), Recipe);
            AssetDatabase.SaveAssets();

            EffectGraphBuilder.Build(Recipe);

            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(Graph);
            var events = new List<string>();
            asset.GetEvents(events);
            CollectionAssert.IsSupersetOf(events, new[] { EffectGraph.BurstEvent, GraphStream.PlayEvent, GraphStream.StopEvent });
            var probe = new GameObject("Probe").AddComponent<VisualEffect>();
            try
            {
                probe.visualEffectAsset = asset;
                var systems = new List<string>();
                probe.GetParticleSystemNames(systems);
                Assert.AreEqual(Kinds().Count() + 1, systems.Count, "every layer and the source's child");
                Assert.IsTrue(EffectGraph.HasDensity(probe));
                Assert.IsTrue(GraphStream.Streams(probe), "the strip streams by rate");
            }
            finally
            {
                Object.DestroyImmediate(probe.gameObject);
            }

            CollectionAssert.IsEmpty(EffectGraphBuilder.ShaderErrors(Graph));
            Assert.IsNull(EffectGraphBuilder.Problem(Recipe));
            File.AppendAllText(Recipe, " ");
            StringAssert.Contains("older than", EffectGraphBuilder.Problem(Recipe));
        }

        [TestCase(TestBurst)]
        [TestCase(TestStream)]
        public void TestGraphs_AreCurrentAndCompile(string recipe)
        {
            Assert.IsNull(EffectGraphBuilder.Problem(recipe));
            CollectionAssert.IsEmpty(EffectGraphBuilder.ShaderErrors(EffectRecipe.GraphPath(recipe)));
        }

        private static IEnumerable<EffectLayerKind> Kinds() => System.Enum.GetValues(typeof(EffectLayerKind)).Cast<EffectLayerKind>();

        // Every optional field set, so every branch of the builder runs.
        private static EffectRecipe AllKinds()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);
            var chunk = AssetDatabase.LoadAssetAtPath<Mesh>(Chunk);
            var recipe = ScriptableObject.CreateInstance<EffectRecipe>();
            foreach (var kind in Kinds())
            {
                var layer = new EffectLayer
                {
                    name = kind.ToString(), kind = kind, capacity = 8, shape = EmitShape.Cone,
                    lifetime = new FloatRange(1f, 2f), size = new FloatRange(1f, 1f),
                    sizeOverLife = AnimationCurve.Linear(0f, 1f, 1f, 2f), alphaOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0f),
                    texture = texture, negativeTexture = texture, motionTexture = texture, emissionRamp = texture,
                    flipbook = new Flipbook { columns = 2, rows = 2, from = 0f, to = 1f, randomStart = kind is EffectLayerKind.Glow or EffectLayerKind.SixWay },
                    opacity = 1.2f, ambient = 0.8f, emissionColor = new Color(4f, 2f, 1f, 1f),
                    heatOverLife = new AnimationCurve(new Keyframe(0f, 1f)), minHeatOverLife = new AnimationCurve(new Keyframe(0f, 0.5f)),
                    meshes = { chunk }, ground = true, groundHeight = -1.5f, bounce = 0.3f, gravity = 1f, length = 2f,
                    wind = 0.5f, drag = 2f, dragBySpeed = true, rim = true, axis = Vector3.forward,
                    noise = new EffectNoise { strength = 0.5f, frequency = 0.6f, scroll = 0.4f }, orbit = new FloatRange(140f, 200f),
                    distortion = 0.6f, scroll = new Vector2(0f, 0.1f),
                };
                Shape(layer);
                Spawn(layer);
                recipe.layers.Add(layer);
            }

            recipe.layers.Add(new EffectLayer
            {
                name = "child", kind = EffectLayerKind.Puff, capacity = 8, shape = EmitShape.Cone, parent = nameof(EffectLayerKind.Source),
                rate = 2f, lifetime = new FloatRange(1f, 1f), size = new FloatRange(1f, 1f), texture = texture,
                flipbook = new Flipbook { columns = 4, rows = 4, from = 0.5f, to = 0.75f, random = true },
            });
            return recipe;
        }

        private static void Shape(EffectLayer layer)
        {
            if (layer.kind == EffectLayerKind.Glow)
            {
                layer.shape = EmitShape.Sphere;
                layer.aspect = 0.6f;
                layer.upright = true;
                layer.pivot = 0.5f;
                layer.drift = new Vector3(0.3f, 0.6f, 0f);
            }

            if (layer.kind == EffectLayerKind.Puff)
            {
                layer.shape = EmitShape.Box;
                layer.shapeScale = new Vector3(1.2f, 0.15f, 1.1f);
            }
        }

        private static void Spawn(EffectLayer layer)
        {
            if (layer.kind == EffectLayerKind.Strip)
            {
                layer.rate = 10f;
                layer.ratePerMetre = 2f;
                return;
            }

            layer.bursts.Add(new EffectBurst { delay = 0f, count = new FloatRange(2f, 4f), repeats = 1, interval = 0.5f });
        }
    }
}
