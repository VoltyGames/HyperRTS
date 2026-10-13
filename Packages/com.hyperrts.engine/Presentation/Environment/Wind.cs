using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Publishes the scene's directional wind as shader globals: <c>_HyperRTSWindDirection</c> (unit XZ, downwind) and
    /// <c>_HyperRTSWindStrength</c>, which flags read. Vegetation wind will extend this with rolling gusts.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(WindZone))]
    [AddComponentMenu(HyperRTSMenu.Environment + "Wind")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class Wind : MonoBehaviour
    {
        private static readonly int DirectionId = Shader.PropertyToID("_HyperRTSWindDirection");
        private static readonly int StrengthId = Shader.PropertyToID("_HyperRTSWindStrength");

        private WindZone _zone;

        private void OnEnable() => _zone = GetComponent<WindZone>();

        private void Update()
        {
            var forward = transform.forward;
            var downwind = new Vector3(forward.x, 0f, forward.z).normalized;
            Shader.SetGlobalVector(DirectionId, new Vector4(downwind.x, 0f, downwind.z, 0f));
            Shader.SetGlobalFloat(StrengthId, _zone.windMain);
        }

        private void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);
    }
}
