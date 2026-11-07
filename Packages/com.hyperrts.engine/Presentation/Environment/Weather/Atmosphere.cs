using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>
    /// One weather's sky, ambient light, ground wetness and vehicle dust and mud colours, on the root of an atmosphere prefab.
    /// Unity's built-in fog stays off, since URP's fog replaces it.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu(HyperRTSMenu.Scene + "Environment/Atmosphere")]
    [Icon(HyperRTSIcons.Atmosphere)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class Atmosphere : PresentationBehaviour
    {
        private static readonly int DustColorId = Shader.PropertyToID("_HyperRTSDustColor");
        private static readonly int MudColorId = Shader.PropertyToID("_HyperRTSMudColor");

        [Tooltip("Sky material; also the source of ambient light.")]
        public Material skybox;

        [Tooltip("Reflection cubemap for this sky.")]
        public Cubemap reflection;

        [Min(0f)]
        public float ambientIntensity = 1f;

        [Min(0f)]
        public float reflectionIntensity = 1f;

        [Tooltip("How wet the ground is: 0 dry, 1 soaked after rain.")]
        [Range(0f, 1f)]
        public float wetness;

        [Tooltip("Dust on vehicles driving dry ground here, as a dry light soil.")]
        public Color dustColor = new(0.45f, 0.4f, 0.33f);

        [Tooltip("Mud on vehicles driving wet ground here, as dark wet earth.")]
        public Color mudColor = new(0.1f, 0.075f, 0.05f);

        private void OnEnable()
        {
            if (!IsComplete())
            {
                return;
            }

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = false;
            Wetness.Publish(wetness);
            Shader.SetGlobalColor(DustColorId, dustColor);
            Shader.SetGlobalColor(MudColorId, mudColor);
            DynamicGI.UpdateEnvironment();
        }

        protected override void OnDisabled() => Wetness.Publish(0f);

        private bool IsComplete()
        {
            if (skybox == null)
            {
                Debug.LogError($"HyperRTS: Atmosphere '{name}' has no skybox material.", this);
                return false;
            }

            if (reflection == null)
            {
                Debug.LogError($"HyperRTS: Atmosphere '{name}' has no reflection cubemap.", this);
                return false;
            }

            return true;
        }
    }
}
