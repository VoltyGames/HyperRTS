using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Plays vertex animation texture clips on this renderer: a HyperRTS/LitTeam material with _VAT on. Game code
    /// switches clips through <see cref="VATClip.Play"/>.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "VAT Animated")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VATAnimatedAuthoring : AuthoringBehaviour
    {
        private const string Keyword = "_VAT";
        private static readonly int PositionsId = Shader.PropertyToID("_VATPositions");
        private static readonly int NormalsId = Shader.PropertyToID("_VATNormals");
        private static readonly int RowsPerFrameId = Shader.PropertyToID("_VATRowsPerFrame");

        // Lightmap indices from here up mean none.
        private const int NoLightmap = 0xFFFE;

        [Tooltip("Clips and all-frame bounds of the vertex animation textures on this renderer's material.")]
        public VATClipTable clips;

        [Tooltip("Clip that plays until game code plays another.")]
        public string startClip;

        /// <summary>Turns a LitTeam material's VAT on and points it at a bake's textures and layout.</summary>
        public static void SetUpMaterial(Material material, Texture positions, Texture normals, int rowsPerFrame)
        {
            material.EnableKeyword(Keyword);
            material.SetTexture(PositionsId, positions);
            material.SetTexture(NormalsId, normals);
            material.SetFloat(RowsPerFrameId, rowsPerFrame);
        }

        public static void AddVAT<TWriter>(ref TWriter writer, in VATClipRange start, AABB bounds)
            where TWriter : struct, IEntityWriter
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

            var materials = renderer.sharedMaterials;
            if (materials.Length != 1)
            {
                return $"needs one material, not {materials.Length}";
            }

            return MaterialProblem(materials[0]);
        }

        private string MaterialProblem(Material material)
        {
            if (material == null || !material.IsKeywordEnabled(Keyword))
            {
                return $"needs a material with the {Keyword} keyword on";
            }

            var positions = material.GetTexture(PositionsId);
            if (positions == null || positions.width != clips.width)
            {
                return $"needs VAT positions {clips.width} texels wide, as '{clips.name}' says";
            }

            var rows = material.GetFloat(RowsPerFrameId);
            if (!Mathf.Approximately(rows, clips.rowsPerFrame))
            {
                return $"has VAT rows per frame {rows} on its material but {clips.rowsPerFrame} in '{clips.name}'";
            }

            return null;
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

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                AddVAT(ref writer, authoring.clips.Range(authoring.startClip), authoring.clips.Bounds);
            }
        }
    }
}
