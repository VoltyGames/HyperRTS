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
            Assert.IsEmpty(EffectRigCheck.Problems(Rig(true, ParticleSystemSimulationSpace.World,
                ParticleSystemScalingMode.Hierarchy)));
        }

        [Test]
        public void RigWithoutEffectRigIsReported()
        {
            var problems = EffectRigCheck.Problems(Rig(false, ParticleSystemSimulationSpace.World,
                ParticleSystemScalingMode.Hierarchy));

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("EffectRig", problems[0]);
        }

        [Test]
        public void LocalSpaceSystemIsReported()
        {
            var problems = EffectRigCheck.Problems(Rig(true, ParticleSystemSimulationSpace.Local,
                ParticleSystemScalingMode.Hierarchy));

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("World space", problems[0]);
        }

        [Test]
        public void NonHierarchyScalingIsReported()
        {
            var problems = EffectRigCheck.Problems(Rig(true, ParticleSystemSimulationSpace.World,
                ParticleSystemScalingMode.Local));

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("Hierarchy scaling", problems[0]);
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
