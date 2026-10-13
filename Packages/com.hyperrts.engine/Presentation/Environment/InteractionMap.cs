using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Owns the match's interaction maps (<see cref="InteractionSurfaces"/>): each frame it fades them (flattened grass
    /// stands up over <see cref="InteractionMath.TrampleRecoverySeconds"/>, ruts fade over
    /// <see cref="InteractionMath.TrackFadeSeconds"/>, crushed crops stay) and draws the stamps
    /// <see cref="InteractionStampSystem"/> gathered. Terrain and foliage read them through Trample.hlsl. Presentation
    /// only, so clients may differ.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Interaction Map")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class InteractionMap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The " + InteractionBrush.ShaderName + " shader the maps are drawn with.")]
        private Shader brush;

        private InteractionBrush _brush;
        private InteractionSurfaces _surfaces;
        private float _recoveryCarry;
        private float _fadeCarry;

        /// <summary>How finely the maps resolve the ground, or Off; the game sets it from its vegetation quality.</summary>
        public static InteractionQuality Quality { get; set; } = InteractionQuality.Full;

        private void Awake() => _brush = new InteractionBrush(brush);

        private void LateUpdate()
        {
            if (!TryGetStamps(out var view, out var stamps))
            {
                Release();
                return;
            }

            Prepare(view.Map.Min, view.Map.Size);
            var deltaTime = Time.deltaTime;
            var recovery = InteractionMath.FadeSteps(ref _recoveryCarry, deltaTime, InteractionMath.TrampleRecoverySeconds);
            var fade = InteractionMath.FadeSteps(ref _fadeCarry, deltaTime, InteractionMath.TrackFadeSeconds);
            _brush.Draw(_surfaces, recovery, fade, stamps);
            _surfaces.Publish();
        }

        private void OnDisable() => Release();

        private void OnDestroy() => _brush?.Dispose();

        private static bool TryGetStamps(out MatchView view, out InteractionStamps stamps)
        {
            stamps = null;
            view = null;
            if (Quality == InteractionQuality.Off)
            {
                return false;
            }

            var ready = MatchView.TryGetDefault(out view);
            if (!ready || !view.HasMap)
            {
                return false;
            }

            stamps = view.EntityManager.World.GetExistingSystemManaged<InteractionStampSystem>()?.Stamps;
            return stamps != null;
        }

        private void Prepare(float2 min, float2 size)
        {
            if (_surfaces != null && _surfaces.Matches(min, size, Quality))
            {
                return;
            }

            Release();
            _surfaces = new InteractionSurfaces(min, size, Quality);
            _brush.Clear(_surfaces);
        }

        private void Release()
        {
            _recoveryCarry = 0f;
            _fadeCarry = 0f;
            if (_surfaces == null)
            {
                return;
            }

            _surfaces.Dispose();
            _surfaces = null;
            InteractionSurfaces.Clear();
        }
    }
}
