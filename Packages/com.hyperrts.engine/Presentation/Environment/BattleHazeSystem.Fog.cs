// URP compiles its Fog override only under this define; without it the haze runs but draws nothing.
#if VOLUMETRIC_FOG
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Environment
{
    public partial class BattleHazeSystem
    {
        // Above the weather's volume, so only the density texture and its tiling change; the rest is the weather's.
        private const float FogPriority = 100f;

        private GameObject _fogHost;
        private VolumeProfile _fogProfile;

        partial void ShowFog(Texture3D texture, float tiling)
        {
            _fogProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _fogProfile.hideFlags = HideFlags.DontSave;
            var fog = _fogProfile.Add<FogVolumeComponent>();
            fog.volumetricFogDensityMode.Override(VolumetricFogDensityMode.Texture);
            fog.volumetricFogDensityTexture.Override(texture);
            fog.volumetricFogDensityTextureTiling.Override(tiling);

            _fogHost = new GameObject("Battle Haze") { hideFlags = HideFlags.DontSave };
            var volume = _fogHost.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = FogPriority;
            volume.sharedProfile = _fogProfile;
        }

        partial void HideFog()
        {
            CoreUtils.Destroy(_fogHost);
            CoreUtils.Destroy(_fogProfile);
            _fogHost = null;
            _fogProfile = null;
        }
    }
}
#endif
