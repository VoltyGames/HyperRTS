using System;
using System.Collections.Generic;
using HyperRTS.Editor.Animation;
using HyperRTS.Presentation.Animation.Bones;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Animation
{
    public partial class BoneRigTests
    {
        private BoneAnimationSet RagdollSet(string handBody)
        {
            var set = Track(ScriptableObject.CreateInstance<BoneAnimationSet>());
            set.name = "Soldier";
            set.ragdoll = new List<RagdollBone>
            {
                new() { bone = "Hips" },
                new() { bone = handBody, parentBone = "Hips" },
            };
            return set;
        }

        [Test]
        public void RagdollBake_WritesParentsBindPosesAndTheBodyEachBoneRidesOn()
        {
            using var rig = Rig();
            var table = Track(ScriptableObject.CreateInstance<BoneClipTable>());

            RagdollSet("Hand").WriteRagdoll(rig, table);

            CollectionAssert.AreEqual(new[] { -1, 0, 0, 1 }, table.parents, "Hips, Hand, OffHand, then the Grip on the hand");
            CollectionAssert.AreEqual(new[] { false, true, false, true }, table.upperBones);
            Assert.AreEqual(0f, table.bindPoses[1].MultiplyPoint3x4(HandAt).magnitude, 1e-4f, "the hand's rest place is its origin");
            Assert.AreEqual(1, table.ragdoll[1].bone);
            Assert.AreEqual(0, table.ragdoll[1].parent);
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, table.boneBodies, "the off hand rides on the hips, the grip on the hand");
        }

        [Test]
        public void RagdollBodyOnAMissingBone_Fails()
        {
            using var rig = Rig();
            var table = Track(ScriptableObject.CreateInstance<BoneClipTable>());

            var error = Assert.Throws<InvalidOperationException>(() => RagdollSet("Foot").WriteRagdoll(rig, table));

            StringAssert.Contains("'Soldier'", error.Message);
        }
    }
}
