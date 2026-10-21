using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Colours and sizes of the world overlays; a game supplies its own asset to the overlay renderer.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Rig + "Overlay Style", fileName = "OverlayStyle")]
    public class OverlayStyle : ScriptableObject
    {
        [Header("Ground Markers")]
        [Tooltip("Marker round the local player's own units.")]
        public Color own = new(0.47f, 0.86f, 0.42f, 1f);

        [Tooltip("Marker round allied units.")]
        public Color ally = new(0.36f, 0.67f, 0.9f, 1f);

        [Tooltip("Marker round hostile units.")]
        public Color enemy = new(0.9f, 0.27f, 0.22f, 1f);

        [Tooltip("Marker round neutral entities (resources, civilians).")]
        public Color neutral = new(0.89f, 0.65f, 0.23f, 1f);

        [Tooltip("Metres of clearance between the footprint and the marker line.")]
        [Min(0f)]
        public float markerMargin = 0.35f;

        [Tooltip("Line width in metres; never thinner than one and a half pixels.")]
        [Min(0.01f)]
        public float markerLine = 0.16f;

        [Tooltip("Metres the soft fill fades over inward from the line.")]
        [Min(0f)]
        public float markerFade = 0.7f;

        [Tooltip("Opacity of the soft fill at the line.")]
        [Range(0f, 1f)]
        public float markerFill = 0.16f;

        [Tooltip("Opacity of a hovered marker; it also draws with a thinner line and no fill.")]
        [Range(0f, 1f)]
        public float hoverOpacity = 0.8f;

        [Tooltip("Seconds a new selection takes to settle from its larger, faded start.")]
        [Min(0.01f)]
        public float selectSeconds = 0.15f;

        [Tooltip("Marker size at the start of the selection animation, relative to its final size.")]
        [Min(1f)]
        public float selectStartScale = 1.18f;

        [Header("Health Bars")]
        [Tooltip("Bar width in pixels at 1080p per metre of unit radius, before the clamp below.")]
        [Min(0f)]
        public float barWidthPerMetre = 12f;

        [Tooltip("Narrowest and widest bar in pixels at 1080p.")]
        public Vector2 barWidthRange = new(36f, 96f);

        [Tooltip("Bar height in pixels at 1080p.")]
        [Min(2f)]
        public float barHeight = 6f;

        [Tooltip("Gap in pixels at 1080p between the top of the model and the bar.")]
        [Min(0f)]
        public float barOffset = 10f;

        [Tooltip("Health one bar segment stands for; squads show a segment per member.")]
        [Min(1f)]
        public float healthPerSegment = 100f;

        [Tooltip("Most segments a bar is cut into.")]
        [Range(1, 20)]
        public int maxSegments = 10;

        [Tooltip("Backing behind the bar and its pips.")]
        public Color healthBack = new(0.02f, 0.03f, 0.03f, 0.82f);

        [Tooltip("Fill at full health.")]
        public Color healthHigh = new(0.52f, 0.8f, 0.36f, 1f);

        [Tooltip("Fill at half health.")]
        public Color healthMid = new(0.93f, 0.74f, 0.23f, 1f);

        [Tooltip("Fill near death.")]
        public Color healthLow = new(0.88f, 0.27f, 0.2f, 1f);

        [Tooltip("Veterancy rank pips.")]
        public Color rankPip = new(0.98f, 0.78f, 0.38f, 1f);

        [Tooltip("Living squad member pips; fallen members show as dim pips.")]
        public Color memberPip = new(0.92f, 0.91f, 0.87f, 1f);

        [Header("Capture & Garrison")]
        [Tooltip("Backing behind the capture dial and the garrison pips.")]
        public Color markerBack = new(0.03f, 0.03f, 0.03f, 0.7f);

        [Tooltip("Unfilled part of the capture dial's ring; the filled part takes the capturer's colour.")]
        public Color dialTrack = new(1f, 1f, 1f, 0.22f);

        [Tooltip("Capture dial radius in pixels at 1080p, the same at every zoom.")]
        [Min(4f)]
        public float dialRadius = 18f;

        [Tooltip("Empty garrison slot; filled slots take the holder's colour.")]
        public Color pipEmpty = new(1f, 1f, 1f, 0.22f);

        [Tooltip("Garrison pip size in pixels at 1080p.")]
        [Min(2f)]
        public float pipSize = 7f;

        [Tooltip("Gap in pixels at 1080p between the top of a building and its markers.")]
        [Min(0f)]
        public float markerOffset = 14f;

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

        public Color RelationColor(Relation relation) => relation switch
        {
            Relation.Own => own,
            Relation.Ally => ally,
            Relation.Enemy => enemy,
            _ => neutral,
        };

        /// <summary>Bar width in reference pixels for a unit of <paramref name="radius"/> metres.</summary>
        public float BarWidth(float radius) =>
            Mathf.Clamp(radius * barWidthPerMetre * 2f, barWidthRange.x, barWidthRange.y);

        /// <summary>Segments for a bar of <paramref name="maxHealth"/>, or one per member for squads.</summary>
        public int Segments(float maxHealth, int members) =>
            members > 0 ? members : Mathf.Clamp(Mathf.CeilToInt(maxHealth / healthPerSegment), 1, maxSegments);
    }
}
