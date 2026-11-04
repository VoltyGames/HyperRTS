using HyperRTS.Presentation.Animation;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>One clip of a <see cref="BoneAnimationSet"/>: how many frames it bakes and the clip time each samples.</summary>
    internal sealed class ClipFrames
    {
        public readonly BoneAnimationSet.Clip Clip;
        private readonly int _steps;

        public ClipFrames(BoneAnimationSet.Clip clip, float fps)
        {
            Clip = clip;
            var steps = Mathf.Max(1, Mathf.RoundToInt(clip.source.length / clip.speed * fps));
            // A loop does not repeat its first frame at the end.
            Count = clip.loop ? steps : steps + 1;
            _steps = steps;
        }

        public int Count { get; }

        /// <summary>Seconds into the source clip that frame <paramref name="frame"/> shows.</summary>
        public float TimeOf(int frame) => Clip.source.length * frame / _steps;

        public VATClipTable.Clip AsClip(int startRow, float fps, float groundSpeed) => new()
        {
            name = Clip.name, startRow = startRow, frameCount = Count, fps = fps, loop = Clip.loop, groundSpeed = groundSpeed,
        };
    }
}
