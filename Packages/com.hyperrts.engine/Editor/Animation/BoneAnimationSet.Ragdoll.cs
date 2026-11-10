using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Animation.Bones;

namespace HyperRTS.Editor.Animation
{
    // The ragdoll bake: what a live pose is rebuilt from, and the bodies resolved to bones.
    public partial class BoneAnimationSet
    {
        /// <summary>Writes every bone's parent, bind pose and body half, and the ragdoll resolved to texture bones.</summary>
        internal void WriteRagdoll(BoneRig posed, BoneClipTable table)
        {
            CheckRagdoll(posed);
            var parents = posed.Parents();
            table.parents = parents.ToList();
            table.bindPoses = posed.BindPoses().ToList();
            table.upperBones = posed.UpperBones().ToList();
            table.ragdoll = ragdoll.Select(body => body.Bake(posed.BoneIndex(body.bone), ParentBody(body))).ToList();
            table.boneBodies = ragdoll.Count == 0 ? new List<int>() : BoneBodies(table.ragdoll, parents);
            table.ragdollTuning = ragdollTuning;
        }

        private int ParentBody(RagdollBone body) =>
            string.IsNullOrEmpty(body.parentBone) ? -1 : ragdoll.FindIndex(other => other.bone == body.parentBone);

        // A bone rides on its own body, else on its nearest ancestor's, else on the root body (body 0).
        private static List<int> BoneBodies(List<RagdollBody> bodies, int[] parents)
        {
            var own = new Dictionary<int, int>();
            for (var i = 0; i < bodies.Count; i++)
            {
                own.Add(bodies[i].bone, i);
            }

            var ridden = new List<int>(parents.Length);
            for (var bone = 0; bone < parents.Length; bone++)
            {
                var at = bone;
                while (at >= 0 && !own.ContainsKey(at))
                {
                    at = parents[at];
                }

                ridden.Add(at >= 0 ? own[at] : 0);
            }

            return ridden;
        }

        private void CheckRagdoll(BoneRig posed)
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < ragdoll.Count; i++)
            {
                var body = ragdoll[i];
                if (!posed.HasBone(body.bone))
                {
                    throw new InvalidOperationException($"Bone set '{name}' ragdoll body {i} names bone '{body.bone}', which the rig lacks.");
                }

                if (!seen.Add(body.bone))
                {
                    throw new InvalidOperationException($"Bone set '{name}' has two ragdoll bodies on bone '{body.bone}'.");
                }
            }

            var parents = posed.Parents();
            for (var i = 0; i < ragdoll.Count; i++)
            {
                CheckParent(posed, parents, i);
            }
        }

        private void CheckParent(BoneRig posed, int[] parents, int index)
        {
            var body = ragdoll[index];
            var root = string.IsNullOrEmpty(body.parentBone);
            if (root != (index == 0))
            {
                throw new InvalidOperationException($"Bone set '{name}' ragdoll needs its root body, the one without a parent, first and only once; body '{body.bone}' breaks that.");
            }

            if (root)
            {
                return;
            }

            if (ParentBody(body) < 0 || !IsAncestor(parents, posed.BoneIndex(body.parentBone), posed.BoneIndex(body.bone)))
            {
                throw new InvalidOperationException($"Bone set '{name}' ragdoll body '{body.bone}' has parent '{body.parentBone}', which is not a body on an ancestor bone.");
            }
        }

        private static bool IsAncestor(int[] parents, int ancestor, int bone)
        {
            for (var at = parents[bone]; at >= 0; at = parents[at])
            {
                if (at == ancestor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
