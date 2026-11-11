using HyperRTS.Presentation.Animation.Bones;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial struct SquadFigureSystem
    {
        /// <summary>
        /// Each part takes its figure's clip change itself, so the clip components are written only in chunks with a part
        /// whose figure changed and every write stays on the part's own entity.
        /// </summary>
        [BurstCompile]
        private struct PartJob : IJobChunk
        {
            public float Now;
            [ReadOnly] public BufferLookup<SquadFigure> Figures;
            public ComponentTypeHandle<FigurePart> Parts;
            public ComponentTypeHandle<LowerClip> Lowers;
            public ComponentTypeHandle<UpperClip> Uppers;
            public ComponentTypeHandle<LowerFade> LowerFades;
            public ComponentTypeHandle<UpperFade> UpperFades;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var parts = chunk.GetNativeArray(ref Parts);
                if (!AnyPlays(parts))
                {
                    return;
                }

                var lowers = chunk.GetNativeArray(ref Lowers);
                var uppers = chunk.GetNativeArray(ref Uppers);
                var lowerFades = chunk.GetNativeArray(ref LowerFades);
                var upperFades = chunk.GetNativeArray(ref UpperFades);
                for (var i = 0; i < parts.Length; i++)
                {
                    if (!Pending(parts[i], out var figure))
                    {
                        continue;
                    }

                    var part = parts[i];
                    part.Applied = figure.ClipEdits;
                    parts[i] = part;
                    if (figure.Figure != part.Model)
                    {
                        continue;
                    }

                    var lower = lowers[i];
                    var upper = uppers[i];
                    var lowerFade = lowerFades[i];
                    var upperFade = upperFades[i];
                    Play(figure, ref lower, ref upper, ref lowerFade, ref upperFade);
                    lowers[i] = lower;
                    uppers[i] = upper;
                    lowerFades[i] = lowerFade;
                    upperFades[i] = upperFade;
                }
            }

            // A part of the model not shown only notes the change: it plays afresh when its model comes back.
            private bool AnyPlays(NativeArray<FigurePart> parts)
            {
                for (var i = 0; i < parts.Length; i++)
                {
                    if (Pending(parts[i], out var figure) && figure.Figure == parts[i].Model)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool Pending(in FigurePart part, out SquadFigure figure)
            {
                figure = default;
                if (!Figures.TryGetBuffer(part.Root, out var figures))
                {
                    return false;
                }

                figure = figures[part.Index];
                return figure.ClipEdits != part.Applied;
            }

            // A range with no frames leaves that half as it is; a rate change keeps each clip playing and its fade running.
            private void Play(in SquadFigure figure, ref LowerClip lower, ref UpperClip upper, ref LowerFade lowerFade,
                ref UpperFade upperFade)
            {
                if (figure.LowerPlayed)
                {
                    lower.Clip.Switch(figure.Lower, figure.LowerStart, Now, figure.LowerRate, ref lowerFade.Fade);
                }

                if (figure.UpperPlayed)
                {
                    upper.Clip.Switch(figure.Upper, figure.UpperStart, Now, figure.UpperRate, ref upperFade.Fade);
                }

                if (!figure.Retimed)
                {
                    return;
                }

                lower.Clip.Value = figure.Lower.Packed(figure.LowerStart, figure.LowerRate);
                if (figure.Upper.SameAs(figure.Lower))
                {
                    upper.Clip.Value = figure.Upper.Packed(figure.LowerStart, figure.LowerRate);
                }
            }
        }
    }
}
