using Unity.VectorGraphics.Editor;
using UnityEditor;

namespace HyperRTS.Editor.Icons
{
    /// <summary>SVGs in <c>Editor/Icons/Glyphs/</c> import as UI Toolkit vector images, so editor UI draws them sharp and tinted.</summary>
    public sealed class GlyphImportRules : AssetPostprocessor
    {
        public const string Folder = "Packages/com.hyperrts.engine/Editor/Icons/Glyphs/";

        private void OnPreprocessAsset()
        {
            if (assetImporter is SVGImporter svg && assetPath.StartsWith(Folder, System.StringComparison.Ordinal))
            {
                svg.SvgType = SVGType.VectorImage;
            }
        }
    }
}
