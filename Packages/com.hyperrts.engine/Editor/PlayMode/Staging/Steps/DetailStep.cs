using System.Linq;
using HyperRTS.Presentation.Quality;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>
    /// Raises render detail past what the play camera needs, for footage shot from far off: shadow reach, plants
    /// included, full vegetation out to longer distances and finer model levels. The run puts every setting back when it ends.
    /// </summary>
    internal sealed class DetailStep : StagingStep
    {
        private const float ShadowReachPriority = 1000f;

        public float? shadows;
        public float? grass;
        public float? crops;
        public float? lod;

        public override void Check()
        {
            if (new[] { shadows, grass, crops, lod }.All(value => value == null))
            {
                throw new System.InvalidOperationException("needs one of 'shadows', 'grass', 'crops' or 'lod'");
            }

            Positive(shadows, "shadows");
            Positive(grass, "grass");
            Positive(crops, "crops");
            Positive(lod, "lod");
        }

        public override void Run(StagingRun run)
        {
            if (shadows is { } reach)
            {
                RaiseShadowReach(run, reach);
            }

            if (lod is { } finer)
            {
                var bias = QualitySettings.lodBias;
                var threshold = QualitySettings.meshLodThreshold;
                QualitySettings.lodBias = bias * finer;
                QualitySettings.meshLodThreshold = threshold / finer;
                run.Undo(() =>
                {
                    QualitySettings.lodBias = bias;
                    QualitySettings.meshLodThreshold = threshold;
                });
            }

            if (shadows != null || grass != null || crops != null)
            {
                RaiseVegetation(run);
            }
        }

        // A global volume above the scene's and the quality level's, so no asset changes.
        private static void RaiseShadowReach(StagingRun run, float reach)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<ShadowsVolumeOverride>().maxShadowDistance.Override(reach);
            var volume = new GameObject("Staging Shadow Reach").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = ShadowReachPriority;
            volume.sharedProfile = profile;
            run.Undo(() =>
            {
                Object.Destroy(volume.gameObject);
                Object.Destroy(profile);
            });
        }

        private void RaiseVegetation(StagingRun run)
        {
            var before = PresentationQuality.Current;
            var raised = before;
            raised.GrassDensity = 1f;
            raised.CropDensity = 1f;
            raised.CloseDetail = 1f;
            raised.GrassDistance = grass ?? before.GrassDistance;
            raised.CropDistance = crops ?? before.CropDistance;
            if (shadows is { } reach)
            {
                // Plants keep their own shadow reach, so without this they stop casting a few dozen metres out.
                raised.GrassShadowDistance = math.min(reach, raised.GrassDistance);
                raised.CropShadowDistance = math.min(reach, raised.CropDistance);
            }

            PresentationQuality.Apply(raised);
            run.Undo(() => PresentationQuality.Apply(before));
        }

        private static void Positive(float? value, string key)
        {
            if (value is <= 0f)
            {
                throw new System.InvalidOperationException($"has a '{key}' that must be above 0, got {value}");
            }
        }
    }
}
