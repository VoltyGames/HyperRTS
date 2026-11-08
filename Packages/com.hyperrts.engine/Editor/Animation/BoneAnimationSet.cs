using HyperRTS.Presentation.Animation.Bones;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// One rig, its clips and the meshes skinned to it, baked into a shared bone texture, clip table and part meshes
    /// in <c>Generated/</c>. Each socket bakes as a bone of its own after the skeleton's.
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

            [Tooltip("Removes the root bone's ground travel, so a clip with root motion plays on the spot; its height and sway stay. The table keeps that travel as the clip's ground speed.")]
            public bool inPlace;

            [Tooltip("Plays this many times faster; over 1 bakes fewer frames.")]
            [Min(0.1f)]
            public float speed = 1f;

            [Tooltip("Both hands hold the gear: every socket with a support swings its gear so the support meets the other hand. Off where a hand lets go, as in a reload, a throw or a fall.")]
            public bool bothHands;
        }

        [Serializable]
        public class Part
        {
            [Tooltip("Name of the mesh asset, after the set's: <Set>_<Part>.")]
            public string name;

            [Tooltip("The model holding the mesh; empty takes it from the rig.")]
            public GameObject model;

            [Tooltip("Nodes of the model merged into this one mesh, such as a soldier's body, head and gear: skinned meshes bound to the rig's skeleton, or plain meshes when Socket is set. They share one material.")]
            public List<string> nodes = new();

            [Tooltip("Empty for a skinned mesh. Else the socket a plain mesh, such as a weapon, sits at, its origin on the socket's node.")]
            public string socket;
        }

        [Serializable]
        public class Socket
        {
            [Tooltip("Name a part asks the socket by.")]
            public string name;

            [Tooltip("A node of the rig: a skeleton bone, or an empty under one where gear sits, such as a rifle's grip in the right hand. Gear follows the bone.")]
            public string node;

            [Tooltip("Empty for gear held in one hand. Else a node under Node where the other hand holds the gear, such as a rifle's handguard: in clips held in both hands the gear swings about Node so this node meets Support Hand.")]
            public string support;

            [Tooltip("The other hand's bone, which Support meets.")]
            public string supportHand;
        }

        [Tooltip("The rig model: the skeleton, the socket nodes and the parts that name no model.")]
        public GameObject rig;

        [Tooltip("The node of the rig holding the skinned mesh whose bones and bind poses every skinned part shares.")]
        public string skeleton;

        public List<Part> parts = new();

        public List<Clip> clips = new();

        [Tooltip("Top bone of the skeleton, such as the hips, and its first bone. Clips play on its parent, in place clips lose its ground travel, and the upper body rides on it.")]
        public string rootBone;

        [Tooltip("Bone at the root of the upper body, which plays its own clip; the bones below it join it.")]
        public string upperRoot;

        public List<Socket> sockets = new();

        [Tooltip("Frames per second sampled.")]
        [Min(1f)]
        public float fps = 30f;

        [Tooltip("Mesh LOD levels Unity generates below each part mesh, drawn by its one renderer by screen size; 0 for none. The vertices stay, so the bone texture still finds them.")]
        [Range(0, 8)]
        public int meshLods;

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
            using var posed = new BoneRig(rig, skeleton, upperRoot, rootBone, sockets);
            var meshes = parts.Select(part => MakeMesh(posed, part)).ToList();
            var bones = posed.BoneNames();
            var pose = BoneFrames.Sample(posed, plans);

            var written = new List<string>
            {
                Write(pose.Texture(bones.Count), TexturePath),
                Write(MakeTable(posed, plans, bones, pose.Bounds(meshes)), TablePath),
            };
            for (var i = 0; i < parts.Count; i++)
            {
                meshes[i].name = $"{name}_{parts[i].name}";
                written.Add(Write(meshes[i], MeshPath(parts[i])));
            }

            AssetDatabase.SaveAssets();
            ReloadDrawnAssets(written);
            return written;
        }

        // Write copies into the loaded assets, which leaves their GPU copies at the old bake; unloading the saved meshes
        // and textures makes the next use read the new ones from disk.
        private static void ReloadDrawnAssets(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset is Mesh or Texture)
                {
                    Resources.UnloadAsset(asset);
                }
            }
        }

        private Mesh MakeMesh(BoneRig posed, Part part)
        {
            var model = part.model != null ? part.model : rig;
            Func<string, Mesh> build = node => posed.SkinnedMesh(model, node);
            if (!string.IsNullOrEmpty(part.socket))
            {
                var socket = sockets.Find(candidate => candidate.name == part.socket)
                    ?? throw new InvalidOperationException($"Bone set '{name}' part '{part.name}' names socket '{part.socket}', which the set lacks.");
                build = node => posed.AttachedMesh(model, node, socket.node);
            }

            var mesh = BoneMesh.Merge(part.nodes.Select(build).ToList());
            if (meshLods > 0)
            {
                MeshLodUtility.GenerateMeshLods(mesh, default, meshLods);
            }

            return mesh;
        }

        private BoneClipTable MakeTable(BoneRig posed, List<ClipFrames> plans, List<string> bones, Bounds bounds)
        {
            var table = CreateInstance<BoneClipTable>();
            table.bones = bones;
            table.frames = plans.Sum(plan => plan.Count);
            table.boundsMin = bounds.min;
            table.boundsMax = bounds.max;
            var start = 0;
            foreach (var plan in plans)
            {
                var ground = plan.Clip.inPlace ? posed.GroundSpeed(plan.Clip.source, plan.Clip.speed) : 0f;
                table.clips.Add(plan.AsClip(start, fps, ground));
                start += plan.Count;
            }

            return table;
        }

        private void Check()
        {
            if (rig == null || string.IsNullOrEmpty(skeleton) || parts.Count == 0 || clips.Count == 0 || string.IsNullOrEmpty(upperRoot) || string.IsNullOrEmpty(rootBone))
            {
                throw new InvalidOperationException($"Bone set '{name}' needs a rig, a skeleton node, parts, clips, a root bone and an upper root bone.");
            }

            var empty = parts.Find(part => string.IsNullOrEmpty(part.name) || part.nodes.Count == 0);
            if (empty != null)
            {
                throw new InvalidOperationException($"Bone set '{name}' has a part without a name or nodes.");
            }

            var missing = clips.Find(clip => clip.source == null || string.IsNullOrEmpty(clip.name));
            if (missing != null)
            {
                throw new InvalidOperationException($"Bone set '{name}' has a clip without a name or a source clip.");
            }
        }

        // Copies into the asset already at the path, so it stays the same object: replacing it would leave prefabs that
        // nest a figure holding a dead reference until they reimport.
        private static string Write(Object made, string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && asset.GetType() == made.GetType())
            {
                var assetName = asset.name;
                EditorUtility.CopySerialized(made, asset);
                asset.name = assetName;
                EditorUtility.SetDirty(asset);
                DestroyImmediate(made);
            }
            else
            {
                AssetDatabase.CreateAsset(made, path);
                asset = AssetDatabase.LoadMainAssetAtPath(path);
            }

            var labels = AssetDatabase.GetLabels(asset);
            if (!labels.Contains(GeneratedLabel))
            {
                AssetDatabase.SetLabels(asset, labels.Append(GeneratedLabel).ToArray());
            }

            return path;
        }
    }
}
