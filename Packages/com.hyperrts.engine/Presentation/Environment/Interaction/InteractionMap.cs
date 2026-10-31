using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Quality;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Interaction
{
    /// <summary>Fades the match's interaction maps and draws their stamps. Presentation only, so clients may differ.</summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Interaction Map")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class InteractionMap : PresentationBehaviour
    {
        [SerializeField]
        [Tooltip("The " + InteractionBrush.ShaderName + " shader the maps are drawn with.")]
        private Shader brush;

        public const float TrampleRecoverySeconds = 60f;
        public const float TrackFadeSeconds = 240f;
        public const float WakeFadeSeconds = 5f;

        private InteractionBrush _brush;
        private InteractionSurfaces _surfaces;
        private float _recoveryCarry;
        private float _fadeCarry;
        private float _wakeCarry;

        protected override void OnAwake() => _brush = new InteractionBrush(brush);

        private void LateUpdate()
        {
            if (!TryGetStamps(out var view, out var stamps))
            {
                Release();
                return;
            }

            Prepare(view.Map.Min, view.Map.Size);
            var deltaTime = Time.deltaTime;
            var recovery = FadeSteps(ref _recoveryCarry, deltaTime, TrampleRecoverySeconds);
            var fade = FadeSteps(ref _fadeCarry, deltaTime, TrackFadeSeconds);
            var wake = FadeSteps(ref _wakeCarry, deltaTime, WakeFadeSeconds);
            _brush.Draw(_surfaces, recovery, fade, wake, stamps);
        }

        protected override void OnDisabled() => Release();

        protected override void OnDestroyed() => _brush?.Dispose();

        private static bool TryGetStamps(out MatchView view, out InteractionStamps stamps)
        {
            stamps = null;
            view = null;
            if (!InteractionStampSystem.MapsOn)
            {
                return false;
            }

            var ready = MatchView.TryGetDefault(out view);
            if (!ready || !view.HasMap)
            {
                return false;
            }

            stamps = InteractionStampSystem.Find(view.EntityManager.World);
            return stamps != null;
        }

        /// <summary>Whole fade steps due for a full fade over <paramref name="duration"/>. <paramref name="carry"/> keeps the remainder.</summary>
        public static int FadeSteps(ref float carry, float deltaTime, float duration)
        {
            carry += deltaTime * InteractionBrush.Levels / duration;
            var steps = (int)math.floor(carry);
            carry -= steps;
            return steps;
        }

        private void Prepare(float2 min, float2 size)
        {
            if (_surfaces != null && _surfaces.Matches(min, size, PresentationQuality.Current.Interactions))
            {
                return;
            }

            Release();
            _surfaces = new InteractionSurfaces(min, size, PresentationQuality.Current.Interactions);
            _surfaces.Publish();
            _brush.Clear(_surfaces);
        }

        private void Release()
        {
            _recoveryCarry = 0f;
            _fadeCarry = 0f;
            _wakeCarry = 0f;
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
