using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// <c>"factory": "colorMaterial"</c>: an untextured HyperRTS/LitTeam material (glass, dark openings) at the item's
    /// <c>material</c> path; it shows team colour only on entities that get one (captured buildings).
    /// </summary>
    public sealed class ColorMaterialFactory : IMapAssetFactory
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        public string Kind => "colorMaterial";

        public IReadOnlyList<string> Generate(MapAssetRequest request)
        {
            var settings = request.Params<ColorMaterialParams>();
            var path = request.Output(request.Refs.material, "material");
            var material = MapAssetFiles.Material(path, "HyperRTS/LitTeam");
            material.SetColor(BaseColor, settings.color);
            material.SetFloat(Smoothness, settings.smoothness);
            return new[] { path };
        }
    }
}
