using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Animation.VAT;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Animation.VAT
{
    /// <summary>Vertex animation clips: row blending, looping, holding and clip changes.</summary>
    public class VATClipTests
    {
        private static readonly ClipRange Walk = new() { StartRow = 10, FrameCount = 4, Fps = 2f, Loop = true };
        private static readonly ClipRange Fall = new() { StartRow = 20, FrameCount = 4, Fps = 2f, Loop = false };

        [Test]
        public void BlendAt_BlendsBetweenFramesFromTheStartTime()
        {
            var frame = PlayingClip.Started(Walk, 5f).BlendAt(5.75f);

            Assert.AreEqual(11, frame.Row);
            Assert.AreEqual(12, frame.NextRow);
            Assert.AreEqual(0.5f, frame.Blend, 1e-5f);
        }

        [Test]
        public void BlendAt_LoopWrapsTheLastFrameIntoTheFirst()
        {
            var clip = PlayingClip.Started(Walk, 0f);

            var last = clip.BlendAt(1.75f);
            var wrapped = clip.BlendAt(2.25f);

            Assert.AreEqual((13, 10), (last.Row, last.NextRow));
            Assert.AreEqual((10, 11), (wrapped.Row, wrapped.NextRow));
        }

        [Test]
        public void BlendAt_OnceHoldsTheLastFrame()
        {
            var frame = PlayingClip.Started(Fall, 0f).BlendAt(100f);

            Assert.AreEqual((23, 23), (frame.Row, frame.NextRow));
            Assert.AreEqual(0f, frame.Blend);
        }

        [Test]
        public void BlendAt_BeforeTheStartShowsTheFirstFrame()
        {
            var frame = PlayingClip.Started(Walk, 10f).BlendAt(9f);

            Assert.AreEqual((10, 11, 0f), (frame.Row, frame.NextRow, frame.Blend));
        }

        [Test]
        public void Switch_FreezesTheShownPoseWithTheSwitchTime()
        {
            var clip = PlayingClip.Started(Walk, 5f);
            var fade = ClipFade.None;

            clip.Switch(Fall, 5.75f, 5.75f, 1f, ref fade);

            Assert.AreEqual(new float4(11f, 12f, 0.5f, 5.75f), fade.Value);
        }
    }
}
