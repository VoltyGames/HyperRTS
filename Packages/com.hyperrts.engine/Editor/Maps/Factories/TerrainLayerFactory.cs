using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// <c>"factory": "terrainLayer"</c>: a TerrainLayer at the item's <c>terrainLayer</c> path from the photo texture
    /// set beside its file. Photos are expected graded at the source, so the diffuse remap stays neutral.
    /// </summary>
    public sealed class TerrainLayerFactory : IMapAssetFactory
    {
        public string Kind => "terrainLayer";

        public IReadOnlyList<string> Generate(MapAssetRequest request)
        {
            var settings = request.Params<TerrainLayerParams>();
            var path = request.Output(request.Refs.terrainLayer, "terrainLayer");
            var (baseColor, normal, mask) = MapTextureSet.Read(request.Folder, settings.textureSet, settings.textureSize);
            var layer = MapAssetFiles.LoadOrCreate(path, () => new TerrainLayer());
            layer.diffuseTexture = baseColor;
            layer.normalMapTexture = normal;
            layer.maskMapTexture = mask;
            layer.tileSize = new Vector2(settings.tileSize, settings.tileSize);
            layer.normalScale = 1f;
            layer.metallic = 0f;
            layer.diffuseRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
            return new[] { path };
        }
    }
}
