using HyperRTS.Core;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial struct SquadFigureSystem
    {
        private partial struct SquadJob
        {
            // The legs follow motion and posture; the upper body fires over them or keeps their clip, in step with them.
            private void Pose(Entity root, ref SquadFigure figure, FigureState moving, Posture posture, bool firing,
                bool digging)
            {
                figure.Posture = posture;
                figure.Firing = firing;
                figure.Digging = digging;
                ref var clips = ref figure.Clips.Value;
                var lower = digging ? clips.Action(MemberAction.Dig) : clips.Lower(posture, moving);
                var upper = digging ? clips.Action(MemberAction.Dig) : clips.Upper(posture, moving, firing);
                var lowerChanged = !figure.Lower.SameAs(lower);
                if (lowerChanged)
                {
                    figure.Lower = lower;
                    figure.LowerRate = figure.RateFor(lower);
                    figure.LowerStart = figure.StartTime(lower, Now, figure.LowerRate);
                }

                var upperChanged = figure.Shot && firing || !figure.Upper.SameAs(upper);
                figure.Upper = upper;
                var upperStart = figure.LowerStart;
                var upperRate = figure.LowerRate;
                if (!upper.SameAs(lower))
                {
                    upperRate = 1f;
                    upperStart = figure.StartTime(upper, Now, upperRate);
                }

                Play(root, figure, lowerChanged ? lower : default, upperChanged ? upper : default, upperStart, upperRate);
            }

            private void Fall(Entity root, ref SquadFigure figure, in ClipRange clip)
            {
                figure.Lower = clip;
                figure.Upper = clip;
                figure.LowerStart = Now;
                figure.LowerRate = 1f;
                Play(root, figure, clip, clip, Now, 1f);
            }

            // A range with no frames leaves that half as it is.
            private void Play(Entity root, in SquadFigure figure, in ClipRange lower, in ClipRange upper,
                float upperStart, float upperRate)
            {
                foreach (var renderer in Renderers[root])
                {
                    if (renderer.Model != figure.Figure)
                    {
                        continue;
                    }

                    var part = renderer.Part;
                    if (lower.FrameCount > 0)
                    {
                        var low = Lowers[part];
                        var fade = LowerFades[part];
                        low.Clip.Switch(lower, figure.LowerStart, Now, figure.LowerRate, ref fade.Fade);
                        Lowers[part] = low;
                        LowerFades[part] = fade;
                    }

                    if (upper.FrameCount > 0)
                    {
                        var high = Uppers[part];
                        var fade = UpperFades[part];
                        high.Clip.Switch(upper, upperStart, Now, upperRate, ref fade.Fade);
                        Uppers[part] = high;
                        UpperFades[part] = fade;
                    }
                }
            }

            // A rate change keeps each clip playing and its fade running; only the speed and start move.
            private void Retime(Entity root, in SquadFigure figure)
            {
                var upperFollows = figure.Upper.SameAs(figure.Lower);
                foreach (var renderer in Renderers[root])
                {
                    if (renderer.Model != figure.Figure)
                    {
                        continue;
                    }

                    var part = renderer.Part;
                    var low = Lowers[part];
                    low.Clip.Value = figure.Lower.Packed(figure.LowerStart, figure.LowerRate);
                    Lowers[part] = low;
                    if (upperFollows)
                    {
                        var high = Uppers[part];
                        high.Clip.Value = figure.Upper.Packed(figure.LowerStart, figure.LowerRate);
                        Uppers[part] = high;
                    }
                }
            }
        }
    }
}
