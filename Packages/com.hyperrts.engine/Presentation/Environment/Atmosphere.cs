using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// One weather's sky: skybox, ambient and reflection light, and ground wetness. Sits on the root of an atmosphere
    /// prefab with its sun, post volume, wind and height fog. Unity's built-in fog stays off; HeightFog replaces it.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu(HyperRTSMenu.Environment + "Atmosphere")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class Atmosphere : PresentationBehaviour
    {
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
