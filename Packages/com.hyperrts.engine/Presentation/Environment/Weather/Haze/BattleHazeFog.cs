using UnityEngine;
#if VOLUMETRIC_FOG
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace HyperRTS.Presentation.Environment.Weather.Haze
{
    /// <summary>The scene volume that draws the haze texture as volumetric fog.</summary>
    internal sealed class BattleHazeFog
    {
#if VOLUMETRIC_FOG
        // Above the weather's volume, so only the density texture and its tiling change; the rest is the weather's.
        private const float FogPriority = 100f;

        private const float VolumetricScale = 0.144765f;

        private GameObject _host;
        private VolumeProfile _profile;
#endif

        // URP compiles its Fog override only under this define; without it the haze runs but draws nothing.
        public void Show(Texture3D texture, float tiling)
        {
#if VOLUMETRIC_FOG
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            var fog = _profile.Add<FogVolumeComponent>();
            fog.volumetricFogDensityMode.Override(VolumetricFogDensityMode.Texture);
            fog.volumetricFogDensityTexture.Override(texture);
            fog.volumetricFogDensityTextureTiling.Override(tiling);

            _host = new GameObject("Battle Haze") { hideFlags = HideFlags.DontSave };
            var volume = _host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = FogPriority;
            volume.sharedProfile = _profile;
#endif
        }

        /// <summary>The fog as the camera's volume stack has it now, so the texture reads against the weather's density and height falloff.</summary>
        public FogShape Shape()
        {
#if VOLUMETRIC_FOG
            var fog = VolumeManager.instance.stack.GetComponent<FogVolumeComponent>();
            var baseHeight = fog.volumetricFogBaseHeight.value;
            // URP drops the density to 0.1% over the layer: scale height is the layer depth over ln 1000.
            var scaleHeight = (fog.volumetricFogMaximumHeight.value - baseHeight) * VolumetricScale;
            return new FogShape(fog.volumetricFogDensity.value, baseHeight, scaleHeight);
#else
            return new FogShape(1f, 0f, 1f);
#endif
        }

        public void Hide()
        {
#if VOLUMETRIC_FOG
            CoreUtils.Destroy(_host);
            CoreUtils.Destroy(_profile);
            _host = null;
            _profile = null;
#endif
        }
    }
}
