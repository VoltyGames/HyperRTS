using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>Effect rig problems: a rig playing a graph by hand passes and each broken setup is named.</summary>
    public class EffectRigTests : TemplateFixture
    {
        private const string TestGraph = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst.vfx";

        [Test]
        public void GraphRigIsValid() => Assert.IsEmpty(EffectRig.Problems(Rig()));

        [TestCase("NoEffectRig", "EffectRig")]
        [TestCase("NoRange", "maxDistance must be above 0")]
        [TestCase("EmptyVariant", "variant 0 is empty")]
        [TestCase("BrokenVariant", "variant 'FX_Test': VFX graph 'Graph' has no asset")]
        [TestCase("NestedVariants", "has variants of its own")]
        [TestCase("GraphWithoutAsset", "VFX graph 'Graph' has no asset")]
        [TestCase("PlaysOnItsOwn", "plays on its own")]
        public void BrokenRigReportsOneProblem(string broken, string expected)
        {
            var problems = EffectRig.Problems(Broken(broken));

            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains(expected, problems[0]);
        }

        private GameObject Broken(string broken)
        {
            switch (broken)
            {
                case "NoEffectRig":
                    return Rig(hasEffectRig: false);
                case "NoRange":
                    var near = Rig();
                    near.GetComponent<EffectRig>().maxDistance = 0f;
                    return near;
                case "EmptyVariant":
                    return WithVariant(Rig(), null);
                case "BrokenVariant":
                    return WithVariant(Rig(), Rig(graph: null));
                case "NestedVariants":
                    var rig = Rig();
                    var variant = Rig();
                    variant.GetComponent<EffectRig>().variants = new[] { rig };
                    return WithVariant(rig, variant);
                case "GraphWithoutAsset":
                    return Rig(graph: null);
                default:
                    var playing = Rig();
                    playing.GetComponentInChildren<VisualEffect>().initialEventName = "OnPlay";
                    return playing;
            }
        }

        private static GameObject WithVariant(GameObject rig, GameObject variant)
        {
            rig.GetComponent<EffectRig>().variants = new[] { variant };
            return rig;
        }

        private GameObject Rig(bool hasEffectRig = true, string graph = TestGraph)
        {
            var root = Track(new GameObject("FX_Test"));
            if (hasEffectRig)
            {
                root.AddComponent<EffectRig>();
            }

            var child = new GameObject("Graph");
            child.transform.SetParent(root.transform);
            var effect = child.AddComponent<VisualEffect>();
            effect.initialEventName = "";
            effect.visualEffectAsset = graph == null ? null : AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graph);
            return root;
        }
    }
}
