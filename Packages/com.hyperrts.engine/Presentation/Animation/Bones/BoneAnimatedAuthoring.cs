using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Simulation.Common.Authoring;
using System;
using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// Plays bone animation clips on this renderer: a mesh imported from a bone kit, on a HyperRTS/LitTeam material with
    /// _BONES on. A figure's body, level-of-detail meshes and weapons each carry one and get the same clips from game
    /// code, which switches them through <see cref="LowerClip"/> and <see cref="UpperClip"/>.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Bone Animated")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BoneAnimatedAuthoring : AuthoringBehaviour
    {
        /// <summary>Mesh channel holding a vertex's four bone indices; the weights, negative for upper-body bones, follow in <see cref="WeightChannel"/>.</summary>
        public const int IndexChannel = 6;

        public const int WeightChannel = 7;

        private const string Keyword = "_BONES";
        private static readonly int BoneTextureId = Shader.PropertyToID("_BoneTex");

        [Tooltip("Bones, clips and all-frame bounds of the bone texture on this renderer's material.")]
        public BoneClipTable clips;

        [Tooltip("Clip both halves of the body play until game code plays another.")]
        public string startClip;

        /// <summary>Turns a LitTeam material's bone skinning on and points it at a bake's texture.</summary>
        public static void SetUpMaterial(Material material, Texture bones)
        {
            material.EnableKeyword(Keyword);
            material.SetTexture(BoneTextureId, bones);
        }

        public static void AddBones(EntityWriter writer, in VATClipRange start, AABB bounds)
        {
            var clip = VATClip.Started(start, 0f);
            writer.Add(new LowerClip { Clip = clip });
            writer.Add(new UpperClip { Clip = clip });
            writer.Add(new LowerFade { Fade = VATFade.None });
            writer.Add(new UpperFade { Fade = VATFade.None });
            writer.Add(new VATBounds { Value = bounds });
        }

        /// <summary>Why this renderer can't play its clips, or null when it can.</summary>
        public string Problem(Mesh mesh, MeshRenderer renderer)
        {
            if (clips == null)
            {
                return "has no bone clip table";
            }

            var hasSkin = mesh != null && mesh.HasVertexAttribute(VertexAttribute.TexCoord6) && mesh.HasVertexAttribute(VertexAttribute.TexCoord7);
            if (!hasSkin)
            {
                return "needs a mesh imported from a bone kit, whose UV6 and UV7 hold its bone indices and weights";
            }

            foreach (var material in renderer.sharedMaterials)
            {
                var problem = MaterialProblem(material);
                if (problem != null)
                {
                    return problem;
                }
            }

            return null;
        }

        private string MaterialProblem(Material material)
        {
            if (material == null || !material.IsKeywordEnabled(Keyword))
            {
                return $"needs a material with the {Keyword} keyword on";
            }

            var texture = material.GetTexture(BoneTextureId);
            if (texture == null || texture.width != clips.TextureWidth || texture.height < clips.frames)
            {
                return $"needs a bone texture {clips.TextureWidth} texels wide and {clips.frames} rows tall, as '{clips.name}' says";
            }

            return null;
        }

        public class Baker : Baker<BoneAnimatedAuthoring>
        {
            public override void Bake(BoneAnimatedAuthoring authoring)
            {
                DependsOn(authoring.clips);
                var mesh = GetComponent<MeshFilter>().sharedMesh;
                var renderer = GetComponent<MeshRenderer>();
                DependsOn(mesh);
                foreach (var material in renderer.sharedMaterials)
                {
                    DependsOn(material);
                }

                var problem = authoring.Problem(mesh, renderer);
                if (problem != null)
                {
                    throw new InvalidOperationException($"Bone Animated on '{authoring.name}' {problem}.");
                }

                var writer = this.Writer();
                AddBones(writer, authoring.clips.Range(authoring.startClip), authoring.clips.Bounds);
            }
        }
    }
}
