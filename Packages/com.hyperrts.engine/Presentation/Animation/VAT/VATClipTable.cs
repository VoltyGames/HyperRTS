using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Animation.Clips;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.VAT
{
    /// <summary>
    /// The width, clips and all-row bounds of one set of vertex animation textures. Import Bake Info fills it from the
    /// bake's <c>VAT/&lt;Model&gt;.vat.json</c>; after that the asset is the source and stays editable.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "VAT Clip Table", fileName = "VATClipTable")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class VATClipTable : ClipTable
    {
        [Tooltip("The bake's VAT/<Model>.vat.json, read by Import Bake Info in the context menu.")]
        public TextAsset bakeInfo;

        [Tooltip("Texture width: the column count the model's UV1 indexes.")]
        [Min(1)]
        public int width = 1;

        /// <summary>Replaces the width, clips and bounds with the ones in a bake info file.</summary>
        public void Import(string json)
        {
            var parsed = VATBakeInfo.Parse(json, name);
            width = parsed.width;
            boundsMin = VATBakeInfo.Corner(parsed.boundsMin);
            boundsMax = VATBakeInfo.Corner(parsed.boundsMax);
            clips = new List<NamedClip>(parsed.clips);
        }

        [ContextMenu("Import Bake Info")]
        private void ImportBakeInfo()
        {
            if (bakeInfo == null)
            {
                throw new InvalidOperationException($"VAT clip table '{name}' has no bake info file to import.");
            }

            Import(bakeInfo.text);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
