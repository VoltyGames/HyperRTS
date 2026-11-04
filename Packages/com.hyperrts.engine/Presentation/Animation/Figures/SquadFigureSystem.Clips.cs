using HyperRTS.Core;
using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial struct SquadFigureSystem
    {
        // The clip playing half of the job: what each figure's renderers play and when.
        private partial struct SquadJob
        {
            // The legs follow motion and posture; the upper body fires over them or keeps their clip, in step with them.
            private void Pose(in FigureParts parts, ref SquadFigure figure, FigureState moving, Posture posture, bool firing,
                bool digging)
            {
                figure.Posture = posture;
                figure.Firing = firing;
                figure.Digging = digging;
                var lower = digging ? figure.Clips.Dig : figure.Clips.Lower(posture, moving);
                var upper = digging ? figure.Clips.Dig : figure.Clips.Upper(posture, moving, firing);
                var lowerChanged = !figure.Lower.SameAs(lower);
                if (lowerChanged)
                {
                    figure.Lower = lower;
                    figure.LowerRate = figure.RateFor(lower);
                    figure.LowerStart = figure.StartTime(lower, Now, figure.LowerRate);
                }

                var upperChanged = !figure.Upper.SameAs(upper);
                figure.Upper = upper;
                var upperStart = figure.LowerStart;
                var upperRate = figure.LowerRate;
                if (!upper.SameAs(lower))
                {
                    upperRate = 1f;
                    upperStart = figure.StartTime(upper, Now, upperRate);
                }

                Play(parts, figure, lowerChanged ? lower : default, upperChanged ? upper : default, upperStart, upperRate);
            }

            private void Fall(in FigureParts parts, ref SquadFigure figure, in VATClipRange clip)
            {
                figure.Lower = clip;
                figure.Upper = clip;
                figure.LowerStart = Now;
                figure.LowerRate = 1f;
                Play(parts, figure, clip, clip, Now, 1f);
            }

            // A range with no frames leaves that half as it is.
            private void Play(in FigureParts parts, in SquadFigure figure, in VATClipRange lower, in VATClipRange upper,
                float upperStart, float upperRate)
            {
                foreach (var part in Gather(parts))
                {
                    if (!PlaysClips(part, figure.Figure))
                    {
                        continue;
                    }

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
            private void Retime(in FigureParts parts, in SquadFigure figure)
            {
                var upperFollows = figure.Upper.SameAs(figure.Lower);
                foreach (var part in Gather(parts))
                {
                    if (!PlaysClips(part, figure.Figure))
                    {
                        continue;
                    }

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

            // The squad's render parts, walked only when a figure first changes this frame.
            private NativeList<Entity> Gather(in FigureParts parts)
            {
                if (parts.List.Length == 0)
                {
                    RenderHierarchy.Collect(parts.Root, Linked, Children, parts.List);
                }

                return parts.List;
            }

            private bool PlaysClips(Entity part, Entity figure) => Lowers.HasComponent(part) && Under(part, figure);

            private bool Under(Entity part, Entity figure)
            {
                for (var step = 0; step < 8 && part != Entity.Null; step++)
                {
                    if (part == figure)
                    {
                        return true;
                    }

                    part = Parents.TryGetComponent(part, out var parent) ? parent.Value : Entity.Null;
                }

                return false;
            }
        }
    }
}
