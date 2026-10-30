using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// The sun shadow of a looping rig's smoke column. Goes on the rig's root; the nearest streamed loops are drawn into
    /// the sun's cloud cookie by <see cref="Environment.CloudShadows"/> from Medium detail up.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Smoke Shadow")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class SmokeShadow : PresentationBehaviour
    {
        [Tooltip("Half the column's width in metres.")]
        public float radius = 4f;

        [Tooltip("Metres the column rises above the loop's socket before it thins out.")]
        public float height = 25f;

        [Tooltip("Share of sunlight the column blocks at its thickest (0 to 1).")]
        [Range(0f, 1f)]
        public float opacity = 0.35f;

        [Tooltip("Seconds the column takes to grow to full size and opacity after the loop starts, as its billows do.")]
        public float growSeconds = 10f;

        /// <summary>
        /// The column over <paramref name="foot"/> after <paramref name="age"/> seconds at the loop's strength: its foot
        /// and radius in <paramref name="start"/>, its top and opacity in <paramref name="end"/>.
        /// </summary>
        public void Column(Vector3 foot, float age, float strength, out Vector4 start, out Vector4 end)
        {
            var grown = growSeconds > 0f ? Mathf.SmoothStep(0f, 1f, age / growSeconds) : 1f;
            var top = foot + Vector3.up * (height * grown);
            start = new Vector4(foot.x, foot.y, foot.z, radius * Mathf.Lerp(0.4f, 1f, grown));
            end = new Vector4(top.x, top.y, top.z, opacity * strength * grown);
        }
    }
}
