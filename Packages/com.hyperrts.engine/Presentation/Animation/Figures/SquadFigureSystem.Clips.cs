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
            private void Pose(ref SquadFigure figure, FigureState moving, Posture posture, bool firing,
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

                figure.MarkPlayed(lowerChanged, upperChanged, upperStart, upperRate);
            }

            private void Fall(ref SquadFigure figure, in ClipRange clip)
            {
                figure.Lower = clip;
                figure.Upper = clip;
                figure.LowerStart = Now;
                figure.LowerRate = 1f;
                figure.MarkPlayed(true, true, Now, 1f);
            }
        }
    }
}
