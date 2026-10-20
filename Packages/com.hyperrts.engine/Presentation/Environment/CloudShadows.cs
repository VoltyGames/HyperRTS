using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Drifts cloud shadows across the battlefield: a tileable cloud picture as the sun's light cookie, sliding with the
    /// wind. Goes on the directional light; the pipeline asset must support light cookies.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light), typeof(UniversalAdditionalLightData))]
    [AddComponentMenu(HyperRTSMenu.Environment + "Cloud Shadows")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class CloudShadows : PresentationBehaviour
    {
        [Tooltip("Tileable greyscale clouds: white lets the sun through, darker is cloud shadow.")]
        public Texture2D clouds;

        [Tooltip("Metres of ground one repeat of the picture covers.")]
        public float size = 600f;

        [Tooltip("Drift in metres per second, along the light's own right and up axes.")]
        public Vector2 wind = new(2.5f, 1.2f);

        private Light _light;
        private UniversalAdditionalLightData _data;

        private void OnEnable() => Apply();

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Apply();
            }
        }

        protected override void OnDisabled()
        {
            if (_light != null)
            {
                _light.cookie = null;
            }
        }

        private void Update()
        {
            var drift = wind * Time.time;
            _data.lightCookieOffset = new Vector2(Mathf.Repeat(drift.x, size), Mathf.Repeat(drift.y, size));
        }

        private void Apply()
        {
            _light = GetComponent<Light>();
            _data = GetComponent<UniversalAdditionalLightData>();
            _light.cookie = clouds;
            _data.lightCookieSize = new Vector2(size, size);
        }
    }
}
