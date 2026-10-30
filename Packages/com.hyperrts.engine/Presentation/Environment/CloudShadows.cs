using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Drifts cloud and smoke shadows across the battlefield: each frame it draws the tileable cloud picture, sliding
    /// with the wind, and the nearest <see cref="SmokeShadows"/> columns into a cookie around the camera that the sun
    /// projects. Goes on the directional light; the pipeline asset must support light cookies.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light), typeof(UniversalAdditionalLightData))]
    [AddComponentMenu(HyperRTSMenu.Environment + "Cloud Shadows")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class CloudShadows : PresentationBehaviour
    {
        /// <summary>Where <c>Resources.Load</c> finds the shader that draws the cookie.</summary>
        public const string ShaderPath = "HyperRTS/CloudCookie";

        private static readonly int CloudsId = Shader.PropertyToID("_Clouds");
        private static readonly int CloudPlaceId = Shader.PropertyToID("_CloudPlace");
        private static readonly int CookieFocusId = Shader.PropertyToID("_CookieFocus");
        private static readonly int WorldToLightId = Shader.PropertyToID("_CookieWorldToLight");

        [Tooltip("Tileable greyscale clouds: white lets the sun through, darker is cloud shadow.")]
        public Texture2D clouds;

        [Tooltip("Metres of ground one repeat of the picture covers.")]
        public float size = 600f;

        [Tooltip("Drift in metres per second, along the light's own right and up axes.")]
        public Vector2 wind = new(2.5f, 1.2f);

        [Tooltip("Metres of ground around the camera the cookie covers. Keep it wider than the match camera sees.")]
        public float area = 640f;

        [Tooltip("Cookie texels along each side; 2048 over 640 m keeps the old picture's 0.3 m detail.")]
        public int resolution = 2048;

        [Tooltip("Draws the shadows of nearby smoke columns (rigs with a Smoke Shadow) into the cookie.")]
        public bool smoke = true;

        private Light _light;
        private UniversalAdditionalLightData _data;
        private RenderTexture _cookie;
        private Material _material;
        private CommandBuffer _commands;
        private Camera _camera;
        private Vector2 _focus;

        /// <summary>The cookie the sun projects, drawn every frame.</summary>
        public RenderTexture Cookie => _cookie;

        private void OnEnable() => Apply();

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Release();
                Apply();
            }
        }

        protected override void OnDisabled()
        {
            if (_light != null)
            {
                _light.cookie = null;
            }

            Release();
        }

        private void Update() => Draw(Time.time);

        /// <summary>Draws the cookie with the clouds drifted for <paramref name="time"/>, around the main camera.</summary>
        public void Draw(float time)
        {
            var worldToLight = transform.worldToLocalMatrix;
            _camera = EffectRange.Main(_camera);
            if (_camera != null)
            {
                _focus = worldToLight.MultiplyPoint3x4(_camera.transform.position);
            }

            var drift = wind * time;
            _material.SetTexture(CloudsId, clouds);
            _material.SetVector(CloudPlaceId, new Vector4(drift.x, drift.y, size, area));
            _material.SetVector(CookieFocusId, new Vector4(_focus.x, _focus.y, resolution, 0f));
            _material.SetMatrix(WorldToLightId, worldToLight);
            _commands.Clear();
            _commands.SetRenderTarget(_cookie);
            _commands.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3);
            var columns = smoke ? Shader.GetGlobalInt(SmokeShadows.CountId) : 0;
            if (columns > 0)
            {
                // Each column is a quad over its footprint, in four copies across the cookie's wrap edges.
                _commands.DrawProcedural(Matrix4x4.identity, _material, 1, MeshTopology.Triangles, 6, columns * 4);
            }

            Graphics.ExecuteCommandBuffer(_commands);
        }

        private void Apply()
        {
            _light = GetComponent<Light>();
            _data = GetComponent<UniversalAdditionalLightData>();
            _material = new Material(Resources.Load<Shader>(ShaderPath)) { hideFlags = HideFlags.DontSave };
            _commands = new CommandBuffer { name = "HyperRTS Cloud Cookie" };
            // Repeat, so the cookie wraps around the camera and only its far seam moves with it.
            _cookie = new RenderTexture(resolution, resolution, GraphicsFormat.R8_UNorm, GraphicsFormat.None)
            {
                name = "Cloud Cookie", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            _cookie.Create();
            _light.cookie = _cookie;
            _data.lightCookieSize = new Vector2(area, area);
            _data.lightCookieOffset = Vector2.zero;
            Draw(Time.time);
        }

        private void Release()
        {
            _commands?.Release();
            _commands = null;
            CoreUtils.Destroy(_material);
            if (_cookie != null)
            {
                _cookie.Release();
                CoreUtils.Destroy(_cookie);
            }

            _cookie = null;
        }
    }
}
