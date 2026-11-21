using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering.TeamOutline
{
    /// <summary>Hides the team outlines that <see cref="TeamOutlineFeature"/> draws, for a player who wants them off.
    /// Every Play session starts with them shown.</summary>
    public static partial class TeamOutlines
    {
        // A shader global, as UnitSilhouettes keeps its own, so the setting reaches every renderer's feature.
        private static readonly int HideId = Shader.PropertyToID("_HyperRTSHideTeamOutlines");

        public static bool Hidden => Shader.GetGlobalFloat(HideId) > 0.5f;

        public static void Hide(bool hidden) => Shader.SetGlobalFloat(HideId, hidden ? 1f : 0f);

        // Shader globals outlive Play mode in the editor.
        [OnEnteringPlayMode]
        private static void Show() => Hide(false);
    }
}
