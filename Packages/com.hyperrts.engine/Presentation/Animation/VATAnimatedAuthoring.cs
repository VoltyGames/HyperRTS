using HyperRTS.Simulation.Common.Authoring;
using System;
using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Plays vertex animation texture clips on this renderer: a HyperRTS/LitTeam material with _VAT_RIGID on.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "VAT Animated")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VATAnimatedAuthoring : AuthoringBehaviour
    {
        private const string RigidKeyword = "_VAT_RIGID";
        private static readonly int RotationsId = Shader.PropertyToID("_VATRotations");
        private static readonly int PositionsId = Shader.PropertyToID("_VATPositions");

        // Lightmap indices from here up mean none.
        private const int NoLightmap = 0xFFFE;

        [Tooltip("Clips and all-frame bounds of the vertex animation textures on this renderer's material.")]
        public VATClipTable clips;

        [Tooltip("Clip that plays until game code plays another.")]
        public string startClip;

        public static void AddVAT(EntityWriter writer, in VATClipRange start, AABB bounds)
        {
            writer.Add(VATClip.Started(start, 0f));
            writer.Add(new VATBounds { Value = bounds });
        }

        /// <summary>Why this renderer can't play its clips, or null when it can.</summary>
        public string Problem(Mesh mesh, MeshRenderer renderer)
        {
            if (clips == null)
            {
                return "has no VAT clip table";
            }

            if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
            {
                return "needs a mesh whose UV1 holds the VAT texel of each vertex";
            }

            // LitTeam's forward pass reads UV1 as the VAT index, not a lightmap UV.
            if (renderer.lightmapIndex is >= 0 and < NoLightmap)
            {
                return "is lightmapped, but VAT renderers use UV1 for the texel index";
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
            if (material == null || !material.IsKeywordEnabled(RigidKeyword))
            {
                return $"needs a material with the {RigidKeyword} keyword on";
            }

            var positions = material.GetTexture(PositionsId);
            if (positions == null || positions.width != clips.width)
            {
                return $"needs VAT positions {clips.width} texels wide, as '{clips.name}' says";
            }

            return material.GetTexture(RotationsId) == null ? "needs VAT rotations on its rigid material" : null;
        }

        public class Baker : Baker<VATAnimatedAuthoring>
        {
            public override void Bake(VATAnimatedAuthoring authoring)
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
                    throw new InvalidOperationException($"VAT Animated on '{authoring.name}' {problem}.");
                }

                var writer = this.Writer();
                AddVAT(writer, authoring.clips.Range(authoring.startClip), authoring.clips.Bounds);
            }
        }
    }
}
