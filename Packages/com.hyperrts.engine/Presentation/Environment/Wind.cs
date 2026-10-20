using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Publishes the scene's wind for flags and vegetation (Wind.hlsl). The <see cref="WindZone"/> gives strength,
    /// turbulence, gust amount (windPulseMagnitude) and gust tiles per second (windPulseFrequency).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(WindZone))]
    [AddComponentMenu(HyperRTSMenu.Environment + "Wind")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class Wind : PresentationBehaviour
    {
        private const int GustMapSize = 128;

        private static readonly int DirectionId = Shader.PropertyToID("_HyperRTSWindDirection");
        private static readonly int StrengthId = Shader.PropertyToID("_HyperRTSWindStrength");
        private static readonly int GustId = Shader.PropertyToID("_HyperRTSWindGust");
        private static readonly int GustOffsetId = Shader.PropertyToID("_HyperRTSWindGustOffset");
        private static readonly int GustMapId = Shader.PropertyToID("_HyperRTSWindGustMap");
        private static readonly int DetailId = Shader.PropertyToID("_HyperRTSWindDetail");

        [Tooltip("Metres of ground one repeat of the gust field covers.")]
        [Min(1f)]
        public float gustSize = 120f;

        [Tooltip("How much trees, grass and crops move in this wind. None keeps them still. Flags still fly.")]
        public WindDetail vegetation = WindDetail.Flutter;

        /// <summary>The last published downwind direction on the ground (xz); zero before any Wind has run.</summary>
        public static Vector3 Downwind { get; private set; }

        private WindZone _zone;
        private Texture2D _gustMap;
        private Vector2 _gustOffset;

        private void OnEnable()
        {
            _zone = GetComponent<WindZone>();
            _gustMap = WindGust.CreateMap(GustMapSize);
            Shader.SetGlobalTexture(GustMapId, _gustMap);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Downwind = Vector3.zero;

        private void Update()
        {
            var forward = transform.forward;
            var downwind = new Vector3(forward.x, 0f, forward.z).normalized;
            var last = _gustOffset;
            _gustOffset = WindGust.Scroll(_gustOffset, new Vector2(downwind.x, downwind.z), _zone.windPulseFrequency, Time.deltaTime);

            Downwind = downwind;
            Shader.SetGlobalVector(DirectionId, downwind);
            Shader.SetGlobalFloat(StrengthId, _zone.windMain);
            Shader.SetGlobalVector(GustId, new Vector4(_zone.windPulseMagnitude, _zone.windTurbulence, 1f / gustSize, 0f));
            Shader.SetGlobalVector(GustOffsetId, new Vector4(_gustOffset.x, _gustOffset.y, last.x, last.y));
            Shader.SetGlobalFloat(DetailId, (float)vegetation);
        }

        protected override void OnDisabled()
        {
            Shader.SetGlobalFloat(StrengthId, 0f);
            Shader.SetGlobalFloat(DetailId, (float)WindDetail.None);
            CoreUtils.Destroy(_gustMap);
        }
    }
}
