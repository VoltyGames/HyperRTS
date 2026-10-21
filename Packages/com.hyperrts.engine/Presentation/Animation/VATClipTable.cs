using System;
using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// The named clips and all-frame bounds of one set of vertex animation textures. Import Bake Info fills it from the
    /// bake's <c>VAT/&lt;Model&gt;.json</c>; after that the asset is the source and stays editable.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "VAT Clip Table", fileName = "VATClipTable")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class VATClipTable : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            [Tooltip("Name game code plays the clip by.")]
            public string name;

            [Tooltip("The clip's first frame; its texture row is this times rows per frame.")]
            [Min(0)]
            public int startRow;

            [Tooltip("Frames in the clip. A loop does not repeat its first frame at the end.")]
            [Min(1)]
            public int frameCount = 1;

            [Tooltip("Frames per second.")]
            [Min(0.01f)]
            public float fps = 30f;

            [Tooltip("Loops; off plays once and holds the last frame.")]
            public bool loop = true;

            public VATClipRange Range => new() { StartRow = startRow, FrameCount = frameCount, Fps = fps, Loop = loop };
        }

        [Tooltip("The bake's VAT/<Model>.json, read by Import Bake Info in the context menu.")]
        [FormerlySerializedAs("sidecar")]
        public TextAsset bakeInfo;

        [Tooltip("Texture width: the column count the model's UV1 indexes.")]
        [Min(1)]
        public int width = 1;

        [Tooltip("Texture rows per frame; a frame wider than the texture wraps onto more rows.")]
        [Min(1)]
        public int rowsPerFrame = 1;

        public List<Clip> clips = new();

        [Tooltip("Object-space minimum over every frame of every clip.")]
        public Vector3 boundsMin;

        [Tooltip("Object-space maximum over every frame of every clip.")]
        public Vector3 boundsMax;

        public AABB Bounds => new MinMaxAABB { Min = boundsMin, Max = boundsMax };

        public VATClipRange Range(string clipName)
        {
            foreach (var clip in clips)
            {
                if (clip.name == clipName)
                {
                    return clip.Range;
                }
            }

            throw new KeyNotFoundException($"VAT clip table '{name}' has no clip '{clipName}'.");
        }

        /// <summary>Replaces the layout, clips and bounds with the ones in a bake info file.</summary>
        public void Import(string json)
        {
            var parsed = VATBakeInfo.Parse(json, name);
            width = parsed.width;
            rowsPerFrame = parsed.rowsPerFrame;
            boundsMin = VATBakeInfo.Corner(parsed.boundsMin);
            boundsMax = VATBakeInfo.Corner(parsed.boundsMax);
            clips = new List<Clip>(parsed.clips);
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
