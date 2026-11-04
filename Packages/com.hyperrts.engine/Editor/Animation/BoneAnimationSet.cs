using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Core;
using HyperRTS.Presentation.Animation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// The source of one bone animation bake: a rig model, the clips to sample on it, the upper-body root bone and the
    /// meshes to skin. <b>Bake</b> writes beside the set, in <c>Generated/</c>, the bone texture
    /// (<c>&lt;Set&gt;_Bones</c>), the clip table (<c>&lt;Set&gt;_Clips</c>) and a mesh for each part
    /// (<c>&lt;Set&gt;_&lt;Part&gt;</c>), whose bone indices and weights sit in vertex channels. Only those reach builds,
    /// never the rig model or its clips.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Bone Animation Set", fileName = "BoneAnimationSet")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class BoneAnimationSet : ScriptableObject
    {
        public const string GeneratedFolder = "Generated";
        public const string GeneratedLabel = "Generated";
        private const int MaxFrames = 16384;

        [Serializable]
        public class Clip
        {
            [Tooltip("Name game code plays the clip by.")]
            public string name;

            [Tooltip("The animation clip to sample on the rig; a humanoid rig retargets clips from other humanoid models.")]
            public AnimationClip source;

            [Tooltip("Loops; off plays once and holds the last frame.")]
            public bool loop = true;

            [Tooltip("Plays this many times faster; over 1 bakes fewer frames.")]
            [Min(0.1f)]
            public float speed = 1f;
        }

        [Serializable]
        public class Part
        {
            [Tooltip("Name of the mesh asset, after the set's: <Set>_<Part>.")]
            public string name;

            [Tooltip("The node of the rig model holding the mesh: a skinned mesh, or a plain mesh when Socket is set.")]
            public string node;

            [Tooltip("Empty for a skinned mesh. Else the socket whose bone a plain mesh, such as a weapon, is weighted to.")]
            public string socket;
        }

        [Serializable]
        public class Socket
        {
            [Tooltip("Name a part asks the socket by.")]
            public string name;

            [Tooltip("The skeleton bone gear on this socket follows.")]
            public string bone;
        }

        [Tooltip("The rig model: a skinned mesh whose bones form the skeleton, and the nodes of the parts.")]
        public GameObject rig;

        [Tooltip("The first part, a skinned mesh, names the skeleton.")]
        public List<Part> parts = new();

        public List<Clip> clips = new();

        [Tooltip("Bone at the root of the upper body, which plays its own clip; the bones below it join it.")]
        public string upperRoot;

        public List<Socket> sockets = new();

        [Tooltip("Frames per second sampled.")]
        [Min(1f)]
        public float fps = 30f;

        public string Folder => $"{Path.GetDirectoryName(AssetDatabase.GetAssetPath(this))}/{GeneratedFolder}".Replace('\\', '/');

        public string TexturePath => $"{Folder}/{name}_Bones.asset";

        public string TablePath => $"{Folder}/{name}_Clips.asset";

        public string MeshPath(Part part) => $"{Folder}/{name}_{part.name}.asset";

        /// <summary>Samples every clip on the rig and writes the generated assets; returns their paths.</summary>
        [ContextMenu("Bake")]
        public List<string> Bake()
        {
            Check();
            var plans = clips.Select(clip => new ClipFrames(clip, fps)).ToList();
            var frames = plans.Sum(plan => plan.Count);
            if (frames > MaxFrames)
            {
                throw new InvalidOperationException($"Bone set '{name}' needs {frames} rows, over the {MaxFrames} texture limit.");
            }

            Directory.CreateDirectory(Folder);
            using var posed = new BoneRig(rig, parts[0].node, upperRoot);
            var meshes = parts.Select(part => MakeMesh(posed, part)).ToList();
            var bones = posed.BoneNames();
            var pose = new BoneFrames();
            foreach (var plan in plans)
            {
                for (var i = 0; i < plan.Count; i++)
                {
                    pose.Add(posed.Pose(plan.Clip.source, plan.TimeOf(i)));
                }
            }

            var written = new List<string>
            {
                Write(pose.Texture(bones.Count), TexturePath),
                Write(MakeTable(plans, bones, pose.Bounds(meshes)), TablePath),
            };
            for (var i = 0; i < parts.Count; i++)
            {
                meshes[i].name = $"{name}_{parts[i].name}";
                written.Add(Write(meshes[i], MeshPath(parts[i])));
            }

            AssetDatabase.SaveAssets();
            return written;
        }

        private Mesh MakeMesh(BoneRig posed, Part part)
        {
            if (string.IsNullOrEmpty(part.socket))
            {
                return posed.SkinnedMesh(part.node);
            }

            var socket = sockets.Find(candidate => candidate.name == part.socket)
                ?? throw new InvalidOperationException($"Bone set '{name}' part '{part.name}' names socket '{part.socket}', which the set lacks.");
            return posed.AttachedMesh(part.node, socket.bone);
        }

        private BoneClipTable MakeTable(List<ClipFrames> plans, List<string> bones, Bounds bounds)
        {
            var table = CreateInstance<BoneClipTable>();
            table.bones = bones;
            table.frames = plans.Sum(plan => plan.Count);
            table.boundsMin = bounds.min;
            table.boundsMax = bounds.max;
            var start = 0;
            foreach (var plan in plans)
            {
                table.clips.Add(plan.AsClip(start, fps));
                start += plan.Count;
            }

            return table;
        }

        private void Check()
        {
            if (rig == null || parts.Count == 0 || clips.Count == 0 || string.IsNullOrEmpty(upperRoot))
            {
                throw new InvalidOperationException($"Bone set '{name}' needs a rig, parts, clips and an upper root bone.");
            }

            var missing = clips.Find(clip => clip.source == null || string.IsNullOrEmpty(clip.name));
            if (missing != null)
            {
                throw new InvalidOperationException($"Bone set '{name}' has a clip without a name or a source clip.");
            }

            if (!string.IsNullOrEmpty(parts[0].socket))
            {
                throw new InvalidOperationException($"Bone set '{name}': its first part must be the skinned mesh that names the skeleton.");
            }
        }

        // Overwrites the file, which keeps its meta and so the references to it.
        private static string Write(Object made, string path)
        {
            AssetDatabase.CreateAsset(made, path);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            var labels = AssetDatabase.GetLabels(asset);
            if (!labels.Contains(GeneratedLabel))
            {
                AssetDatabase.SetLabels(asset, labels.Append(GeneratedLabel).ToArray());
            }

            return path;
        }
    }
}
