using System;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Colours and sizes of the world overlays.</summary>
    [Serializable]
    public class OverlayStyle
    {
        [Header("Selection Rings")]
        [Tooltip("Ring under your own and allied selected entities.")]
        public Color ownRing = new(0.25f, 1f, 0.35f, 0.9f);

        [Tooltip("Ring under selected hostile entities.")]
        public Color enemyRing = new(1f, 0.25f, 0.2f, 0.9f);

        [Tooltip("Ring under selected neutral entities (resources, civilians).")]
        public Color neutralRing = new(1f, 0.9f, 0.25f, 0.9f);

        [Tooltip("Ring radius relative to the entity's nav radius or footprint.")]
        [Min(1f)]
        public float ringScale = 1.25f;

        [Header("Health Bars")]
        [Tooltip("Bar background.")]
        public Color healthBack = new(0f, 0f, 0f, 0.65f);

        [Tooltip("Fill above 60% health.")]
        public Color healthHigh = new(0.3f, 0.95f, 0.3f, 1f);

        [Tooltip("Fill between 30% and 60% health.")]
        public Color healthMid = new(1f, 0.85f, 0.2f, 1f);

        [Tooltip("Fill below 30% health.")]
        public Color healthLow = new(1f, 0.25f, 0.2f, 1f);

        [Tooltip("Bar thickness in world units.")]
        [Min(0.01f)]
        public float barHeight = 0.18f;

        [Tooltip("Gap between the top of the mesh and the bar.")]
        public float barOffset = 0.5f;

        [Header("Placement & Rally")]
        [Tooltip("Ghost box where the building can be placed.")]
        public Color placementValid = new(0.25f, 1f, 0.35f, 0.35f);

        [Tooltip("Ghost box where the building can't be placed.")]
        public Color placementInvalid = new(1f, 0.2f, 0.2f, 0.35f);

        [Tooltip("Height of the placement ghost box.")]
        [Min(0.1f)]
        public float ghostHeight = 1.5f;

        [Tooltip("Rally point marker and line.")]
        public Color rally = new(0.3f, 0.8f, 1f, 0.85f);
    }
}
