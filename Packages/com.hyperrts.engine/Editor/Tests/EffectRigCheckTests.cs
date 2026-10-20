using HyperRTS.Editor.Validation.Rules;
using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class EffectRigCheckTests : TemplateFixture
    {
        [Test]
        public void WorldSpaceHierarchyRigIsValid()
        {
            Assert.IsEmpty(EffectRig.Problems(Rig(true, ParticleSystemSimulationSpace.World,
                ParticleSystemScalingMode.Hierarchy)));
        }

        [Test]
        public void RigWithoutEffectRigIsReported()
        {
            var problems = EffectRig.Problems(Rig(false, ParticleSystemSimulationSpace.World,
                ParticleSystemScalingMode.Hierarchy));

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("EffectRig", problems[0]);
        }

        [Test]
        public void LocalSpaceSystemIsReported()
        {
            var problems = EffectRig.Problems(Rig(true, ParticleSystemSimulationSpace.Local,
                ParticleSystemScalingMode.Hierarchy));

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("World space", problems[0]);
        }

        [Test]
        public void NonHierarchyScalingIsReported()
        {
            var problems = EffectRig.Problems(Rig(true, ParticleSystemSimulationSpace.World,
                ParticleSystemScalingMode.Local));

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("Hierarchy scaling", problems[0]);
        }

        [Test]
        public void EmptyVariantIsReported()
        {
            var rig = Rig(true, ParticleSystemSimulationSpace.World, ParticleSystemScalingMode.Hierarchy);
            rig.GetComponent<EffectRig>().variants = new GameObject[] { null };

            var problems = EffectRig.Problems(rig);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("variant 0 is empty", problems[0]);
        }

        [Test]
        public void BrokenVariantIsReported()
        {
            var rig = Rig(true, ParticleSystemSimulationSpace.World, ParticleSystemScalingMode.Hierarchy);
            var variant = Rig(true, ParticleSystemSimulationSpace.Local, ParticleSystemScalingMode.Hierarchy);
            rig.GetComponent<EffectRig>().variants = new[] { variant };

            var problems = EffectRig.Problems(rig);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("variant 'FX_Test': particle system 'Burst' does not simulate in World space",
                problems[0]);
        }

        [Test]
        public void VariantWithVariantsIsReported()
        {
            var rig = Rig(true, ParticleSystemSimulationSpace.World, ParticleSystemScalingMode.Hierarchy);
            var variant = Rig(true, ParticleSystemSimulationSpace.World, ParticleSystemScalingMode.Hierarchy);
            rig.GetComponent<EffectRig>().variants = new[] { variant };
            variant.GetComponent<EffectRig>().variants = new[] { rig };

            var problems = EffectRig.Problems(rig);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("has variants of its own", problems[0]);
        }

        [Test]
        public void EndlessBurstIsReported()
        {
            var rig = Rig(true, ParticleSystemSimulationSpace.World, ParticleSystemScalingMode.Hierarchy);
            var emission = rig.GetComponentInChildren<ParticleSystem>().emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 1, 0, 1f) });

            var problems = EffectRig.Problems(rig);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("repeats forever", problems[0]);
        }

        private GameObject Rig(bool hasEffectRig, ParticleSystemSimulationSpace space, ParticleSystemScalingMode scaling)
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
