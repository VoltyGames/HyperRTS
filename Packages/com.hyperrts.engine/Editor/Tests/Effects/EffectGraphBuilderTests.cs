using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Effects;
using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>
    /// A spec with one layer of every kind, a streamed strip and a child of a source builds a graph the pool can play,
    /// and the stamp finds stale graphs.
    /// </summary>
    public class EffectGraphBuilderTests
    {
        private const string Folder = "Assets/_EffectGraphBuilderTests";
        private const string Spec = Folder + "/AllKinds.effect.json";
        private const string Graph = Folder + "/AllKinds.vfx";
        private const string Chunk = Folder + "/Chunk.asset";
        private const string Texture = "Packages/com.hyperrts.engine/Presentation/Environment/EdgeNoise.png";
        private const string TestBurst = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst.effect.json";
        private const string TestStream = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestStream.effect.json";

        private static readonly string[] Kinds = { "glow", "puff", "sixway", "ring", "debris", "spark", "strip", "distort", "source" };

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void EveryLayerKind_BuildsAGraphWithItsEventsAndProperties()
        {
            AssetDatabase.CreateFolder("Assets", "_EffectGraphBuilderTests");
            AssetDatabase.CreateAsset(Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx")), Chunk);
            File.WriteAllText(Spec, AllKinds());
            AssetDatabase.ImportAsset(Spec);

            EffectGraphBuilder.Build(Spec);

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
                Assert.AreEqual(Kinds.Length + 1, systems.Count, "every layer and the source's child");
                Assert.IsTrue(EffectGraph.HasDensity(probe));
                Assert.IsTrue(GraphStream.Streams(probe), "the strip streams by rate");
            }
            finally
            {
                Object.DestroyImmediate(probe.gameObject);
            }

            Assert.IsNull(EffectGraphBuilder.Problem(Spec));
            File.AppendAllText(Spec, " ");
            StringAssert.Contains("older than", EffectGraphBuilder.Problem(Spec));
        }

        [TestCase(TestBurst)]
        [TestCase(TestStream)]
        public void TestGraphs_AreCurrent(string spec) => Assert.IsNull(EffectGraphBuilder.Problem(spec));

        private static string AllKinds()
        {
            var layers = new List<string>();
            foreach (var kind in Kinds)
            {
                var shape = kind == "ring" ? "" : @"""shape"": ""cone"", ";
                var spawn = kind == "strip" ? @"""rate"": 10, ""ratePerMetre"": 2," : @"""bursts"": [ { ""delay"": 0, ""count"": { ""min"": 2, ""max"": 4 }, ""repeats"": 1, ""interval"": 0.5 } ],";
                layers.Add($@"{{ ""name"": ""{kind}"", ""kind"": ""{kind}"", ""capacity"": 8, {shape} {spawn}
                    ""lifetime"": {{ ""min"": 1, ""max"": 2 }}, ""size"": {{ ""min"": 1, ""max"": 1 }},
                    ""sizeOverLife"": [ {{ ""time"": 0, ""value"": 1 }}, {{ ""time"": 1, ""value"": 2 }} ],
                    ""alphaOverLife"": [ {{ ""time"": 0, ""value"": 1 }}, {{ ""time"": 1, ""value"": 0 }} ],
                    ""texture"": ""{Texture}"", ""negativeTexture"": ""{Texture}"", ""motionTexture"": ""{Texture}"",
                    ""flipbook"": {{ ""columns"": 2, ""rows"": 2, ""from"": 0, ""to"": 1 }},
                    ""emission"": 2, ""heatOverLife"": [ {{ ""time"": 0, ""value"": 1 }} ],
                    ""mesh"": ""{Chunk}"", ""bounce"": 0.3, ""gravity"": 1, ""length"": 2,
                    ""wind"": 0.5, ""rim"": true, ""distortion"": 0.6, ""scroll"": {{ ""x"": 0, ""y"": 0.1 }} }}");
            }

            layers.Add($@"{{ ""name"": ""child"", ""kind"": ""puff"", ""capacity"": 8, ""shape"": ""cone"", ""parent"": ""source"", ""rate"": 2,
                ""lifetime"": {{ ""min"": 1, ""max"": 1 }}, ""size"": {{ ""min"": 1, ""max"": 1 }}, ""texture"": ""{Texture}"" }}");

            return $@"{{ ""layers"": [ {string.Join(", ", layers)} ] }}";
        }
    }
}
