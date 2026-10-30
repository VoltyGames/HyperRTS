using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>Effect rig problems: a world-space, hierarchy-scaled rig passes and each broken setup is named.</summary>
    public class EffectRigTests : TemplateFixture
    {
        private const ParticleSystemSimulationSpace World = ParticleSystemSimulationSpace.World;
        private const ParticleSystemScalingMode Hierarchy = ParticleSystemScalingMode.Hierarchy;

        [Test]
        public void WorldSpaceHierarchyRigIsValid() => Assert.IsEmpty(EffectRig.Problems(Rig()));

        [TestCase("NoEffectRig", "EffectRig")]
        [TestCase("LocalSpace", "World space")]
        [TestCase("LocalScaling", "Hierarchy scaling")]
        [TestCase("EmptyVariant", "variant 0 is empty")]
        [TestCase("BrokenVariant", "variant 'FX_Test': particle system 'Burst' does not simulate in World space")]
        [TestCase("NestedVariants", "has variants of its own")]
        [TestCase("EndlessBurst", "repeats forever")]
        [TestCase("GraphWithoutAsset", "VFX graph 'Graph' has no asset")]
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
                case "LocalSpace":
                    return Rig(space: ParticleSystemSimulationSpace.Local);
                case "LocalScaling":
                    return Rig(scaling: ParticleSystemScalingMode.Local);
                case "EmptyVariant":
                    return WithVariant(Rig(), null);
                case "BrokenVariant":
                    return WithVariant(Rig(), Rig(space: ParticleSystemSimulationSpace.Local));
                case "NestedVariants":
                    var rig = Rig();
                    var variant = Rig();
                    variant.GetComponent<EffectRig>().variants = new[] { rig };
                    return WithVariant(rig, variant);
                case "GraphWithoutAsset":
                    var graphRig = Rig();
                    var graph = new GameObject("Graph");
                    graph.transform.SetParent(graphRig.transform);
                    graph.AddComponent<VisualEffect>();
                    return graphRig;
                default:
                    var endless = Rig();
                    var emission = endless.GetComponentInChildren<ParticleSystem>().emission;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 1, 0, 1f) });
                    return endless;
            }
        }

        private static GameObject WithVariant(GameObject rig, GameObject variant)
        {
            rig.GetComponent<EffectRig>().variants = new[] { variant };
            return rig;
        }

        private GameObject Rig(bool hasEffectRig = true, ParticleSystemSimulationSpace space = World,
            ParticleSystemScalingMode scaling = Hierarchy)
        {
            var root = Track(new GameObject("FX_Test"));
            if (hasEffectRig)
            {
                root.AddComponent<EffectRig>();
            }

            var child = new GameObject("Burst");
            child.transform.SetParent(root.transform);
            var main = child.AddComponent<ParticleSystem>().main;
            main.simulationSpace = space;
            main.scalingMode = scaling;
            return root;
        }
    }
}
