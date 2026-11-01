using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>The Effect Preview's remembered controls, kept in the editor state.</summary>
    [Serializable]
    public class EffectPreviewSettings
    {
        public bool follow = true;

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
