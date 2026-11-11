using UnityEngine;
#if VOLUMETRIC_FOG
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>The scene volume that draws the haze texture as volumetric fog.</summary>
    internal sealed class BattleHazeFog
    {
#if VOLUMETRIC_FOG
        // Above the weather's volume, so only the density texture and its tiling change; the rest is the weather's.
        private const float FogPriority = 100f;

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
