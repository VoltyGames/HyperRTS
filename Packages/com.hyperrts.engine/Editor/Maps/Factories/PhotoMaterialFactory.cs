using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// <c>"factory": "photoMaterial"</c>: a URP Lit material at the item's <c>material</c> path from the photo texture
    /// set beside its file (base colour, normal, and the mask map as metallic-smoothness and occlusion).
    /// </summary>
    public sealed class PhotoMaterialFactory : IMapAssetFactory
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicMap = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        public string Kind => "photoMaterial";

        public IReadOnlyList<string> Generate(MapAssetRequest request)
        {
            var settings = request.Params<PhotoMaterialParams>();
            var path = request.Output(request.Refs.material, "material");
            var (folder, set, size) = (request.Folder, settings.textureSet, settings.textureSize);
            var baseColor = MapTextureSet.BaseColor(folder, set, size);
            var normal = MapTextureSet.Normal(folder, set, size);
            var mask = MapTextureSet.Mask(folder, set, size);
            var material = MapAssetFiles.Material(path, "Universal Render Pipeline/Lit");
            material.SetTexture(BaseMap, baseColor);
            material.SetTexture(BumpMap, normal);
            material.SetTexture(MetallicMap, mask);
            material.SetTexture(OcclusionMap, mask);
            material.SetFloat(Smoothness, settings.smoothness);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            return new[] { path };
        }
    }
}
