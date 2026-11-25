using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>The Effects page's remembered controls.</summary>
    [FilePath("UserSettings/HyperRTSEffectPreview.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class EffectPreviewSettings : UserSettings<EffectPreviewSettings>
    {
        public bool follow = true;

        /// <summary>Asset path of the effect rig shown last.</summary>
        public string lastEffect = "";

        public bool loop = true;

        [Range(0.5f, 10f)]
        public float interval = 4f;

        [Range(0.25f, 4f)]
        public float scale = 1f;

        [Range(0f, 5f)]
        public float windStrength;

        [Range(0f, 360f)]
        public float windHeading;
    }
}
