using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Animation.Bones;

namespace HyperRTS.Editor.Animation
{
    /// <summary>A bone set's ragdoll bodies resolved to texture bones: what a live pose is rebuilt from.</summary>
    internal sealed class RagdollBake
    {
        private readonly string _setName;
        private readonly List<RagdollBone> _bodies;
        private readonly RagdollTuning _tuning;

        public RagdollBake(string setName, List<RagdollBone> bodies, RagdollTuning tuning)
        {
            (_setName, _bodies, _tuning) = (setName, bodies, tuning);
        }

        /// <summary>Writes every bone's parent, bind pose and body half, and the ragdoll resolved to texture bones.</summary>
        public void Write(BoneRig posed, BoneClipTable table)
        {
            CheckRagdoll(posed);
            var parents = posed.Parents();
            table.parents = parents.ToList();
            table.bindPoses = posed.BindPoses().ToList();
            table.upperBones = posed.UpperBones().ToList();
            table.ragdoll = _bodies.Select(body => body.Bake(posed.BoneIndex(body.bone), ParentBody(body))).ToList();
            table.boneBodies = _bodies.Count == 0 ? new List<int>() : BoneBodies(table.ragdoll, parents);
            table.ragdollTuning = _tuning;
        }

        private int ParentBody(RagdollBone body) =>
            string.IsNullOrEmpty(body.parentBone) ? -1 : _bodies.FindIndex(other => other.bone == body.parentBone);

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
            for (var i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (!posed.HasBone(body.bone))
                {
                    throw new InvalidOperationException($"Bone set '{_setName}' ragdoll body {i} names bone '{body.bone}', which the rig lacks.");
                }

                if (!seen.Add(body.bone))
                {
                    throw new InvalidOperationException($"Bone set '{_setName}' has two ragdoll bodies on bone '{body.bone}'.");
                }
            }

            var parents = posed.Parents();
            for (var i = 0; i < _bodies.Count; i++)
            {
                CheckParent(posed, parents, i);
            }
        }

        private void CheckParent(BoneRig posed, int[] parents, int index)
        {
            var body = _bodies[index];
            var root = string.IsNullOrEmpty(body.parentBone);
            if (root != (index == 0))
            {
                throw new InvalidOperationException($"Bone set '{_setName}' ragdoll needs its root body, the one without a parent, first and only once; body '{body.bone}' breaks that.");
            }

            if (root)
            {
                return;
            }

            if (ParentBody(body) < 0 || !IsAncestor(parents, posed.BoneIndex(body.parentBone), posed.BoneIndex(body.bone)))
            {
                throw new InvalidOperationException($"Bone set '{_setName}' ragdoll body '{body.bone}' has parent '{body.parentBone}', which is not a body on an ancestor bone.");
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
