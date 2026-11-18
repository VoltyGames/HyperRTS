using System.Linq;
using HyperRTS.Presentation.Quality;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>
    /// Raises render detail past what the play camera needs, for footage shot from far off: shadow reach, plants
    /// included, full vegetation out to longer distances and finer model levels. The run puts every setting back when it ends.
    /// </summary>
    internal sealed class DetailStep : StagingStep
    {
        private readonly float? _shadows;
        private readonly float? _grass;
        private readonly float? _crops;
        private readonly float? _lod;

        private DetailStep(StagingObject json)
        {
            _shadows = Positive(json, "shadows");
            _grass = Positive(json, "grass");
            _crops = Positive(json, "crops");
            _lod = Positive(json, "lod");
            if (new[] { _shadows, _grass, _crops, _lod }.All(value => value == null))
            {
                throw json.Problem("needs one of 'shadows', 'grass', 'crops' or 'lod'");
            }
        }

        public override string Kind => "detail";

        public static StagingStep Read(StagingObject step) => new DetailStep(step.Child("detail"));

        public override void Run(StagingRun run)
        {
            if (_shadows is { } shadows)
            {
                var pipeline = UniversalRenderPipeline.asset;
                var reach = pipeline.shadowDistance;
                pipeline.shadowDistance = shadows;
                run.Undo(() => pipeline.shadowDistance = reach);
            }

            if (_lod is { } lod)
            {
                var bias = QualitySettings.lodBias;
                var threshold = QualitySettings.meshLodThreshold;
                QualitySettings.lodBias = bias * lod;
                QualitySettings.meshLodThreshold = threshold / lod;
                run.Undo(() =>
                {
                    QualitySettings.lodBias = bias;
                    QualitySettings.meshLodThreshold = threshold;
                });
            }

            if (_shadows != null || _grass != null || _crops != null)
            {
                RaiseVegetation(run);
            }
        }

        private void RaiseVegetation(StagingRun run)
        {
            var before = PresentationQuality.Current;
            var raised = before;
            raised.GrassDensity = 1f;
            raised.CropDensity = 1f;
            raised.CloseDetail = 1f;
            raised.GrassDistance = _grass ?? before.GrassDistance;
            raised.CropDistance = _crops ?? before.CropDistance;
            if (_shadows is { } shadows)
            {
                // Plants keep their own shadow reach, so without this they stop casting a few dozen metres out.
                raised.GrassShadowDistance = math.min(shadows, raised.GrassDistance);
                raised.CropShadowDistance = math.min(shadows, raised.CropDistance);
            }

            PresentationQuality.Apply(raised);
            run.Undo(() => PresentationQuality.Apply(before));
        }

        private static float? Positive(StagingObject json, string key)
        {
            var value = json.NumberOrNull(key);
            return value is null or > 0f ? value : throw json.Problem($"'{key}' must be above 0, got {value}");
        }
    }
}
