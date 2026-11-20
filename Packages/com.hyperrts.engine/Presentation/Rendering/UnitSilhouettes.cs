using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Hides the team-colour silhouettes of units behind cover, for footage. URP's Render Objects features on the units
    /// rendering layer draw them; every Play session starts with them shown.
    /// </summary>
    public static partial class UnitSilhouettes
    {
        /// <summary>The LightMode of LitTeam's pass that marks a unit's visible pixels in the stencil.</summary>
        public const string MaskPass = "HyperRTSUnitMask";

        /// <summary>The LightMode of LitTeam's pass that paints a unit's hidden parts in its team colour.</summary>
        public const string SilhouettePass = "HyperRTSSilhouette";

        private static readonly int HideId = Shader.PropertyToID("_HyperRTSHideSilhouettes");

        public static void Hide(bool hidden) => Shader.SetGlobalFloat(HideId, hidden ? 1f : 0f);

        // Shader globals outlive Play mode in the editor.
        [OnEnteringPlayMode]
        private static void Show() => Hide(false);
    }
}
