using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using Unity.Mathematics;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Fades the match's interaction maps and draws their stamps. Presentation only, so clients may differ.</summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Interaction Map")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed partial class InteractionMap : PresentationBehaviour
    {
        [SerializeField]
        [Tooltip("The " + InteractionBrush.ShaderName + " shader the maps are drawn with.")]
        private Shader brush;

        private InteractionBrush _brush;
        private InteractionSurfaces _surfaces;
        private float _recoveryCarry;
        private float _fadeCarry;
        private float _wakeCarry;

        /// <summary>Map resolution, or Off. Written by <see cref="Rendering.PresentationQuality.Apply"/>.</summary>
        [AutoStaticsCleanup]
        public static InteractionQuality Quality { get; internal set; } = PresentationQuality.Default.Interactions;

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
            var recovery = InteractionMath.FadeSteps(ref _recoveryCarry, deltaTime, InteractionMath.TrampleRecoverySeconds);
            var fade = InteractionMath.FadeSteps(ref _fadeCarry, deltaTime, InteractionMath.TrackFadeSeconds);
            var wake = InteractionMath.FadeSteps(ref _wakeCarry, deltaTime, InteractionMath.WakeFadeSeconds);
            _brush.Draw(_surfaces, recovery, fade, wake, stamps);
        }

        protected override void OnDisabled() => Release();

        protected override void OnDestroyed() => _brush?.Dispose();

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
