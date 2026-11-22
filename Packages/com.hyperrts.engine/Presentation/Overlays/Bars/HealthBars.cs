using UnityEngine;

namespace HyperRTS.Presentation.Overlays.Bars
{
    /// <summary>One frame's screen-aligned health bars, all in one draw.</summary>
    public sealed class HealthBars : OverlayBatch<HealthBarInstance>
    {
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int BackId = Shader.PropertyToID("_Back");
        private static readonly int HighId = Shader.PropertyToID("_High");
        private static readonly int MidId = Shader.PropertyToID("_Mid");
        private static readonly int LowId = Shader.PropertyToID("_Low");
        private static readonly int RankPipId = Shader.PropertyToID("_RankPip");
        private static readonly int MemberPipId = Shader.PropertyToID("_MemberPip");
        private static readonly int SuppressedId = Shader.PropertyToID("_Suppressed");
        private static readonly int PinnedId = Shader.PropertyToID("_Pinned");
        private static readonly int RetreatId = Shader.PropertyToID("_Retreat");
        private static readonly int HeavyCoverId = Shader.PropertyToID("_HeavyCover");
        private static readonly int LightCoverId = Shader.PropertyToID("_LightCover");

        public HealthBars(Mesh billboard, Material material) : base(billboard, material)
        {
        }

        public void Add(Vector3 anchor, in HealthBarData bar, OverlayStyle style)
        {
            Add(new HealthBarInstance
            {
                Bar = new Vector4(bar.Fraction, bar.Segments, bar.Rank, 1f),
                Size = new Vector4(bar.Width, style.barHeight, bar.Squad.Members, bar.Squad.Alive),
                Squad = new Vector4(bar.Squad.Suppression, (float)bar.Squad.Level, (float)bar.Squad.Cover,
                    bar.Squad.Retreating ? 1f : 0f),
                Anchor = anchor,
            });
        }

        protected override void ApplyStyle(OverlayStyle style, Material material)
        {
            material.SetFloat(OffsetId, style.barOffset);
            material.SetColor(BackId, style.healthBack);
            material.SetColor(HighId, style.healthHigh);
            material.SetColor(MidId, style.healthMid);
            material.SetColor(LowId, style.healthLow);
            material.SetColor(RankPipId, style.rankPip);
            material.SetColor(MemberPipId, style.memberPip);
            material.SetColor(SuppressedId, style.suppressed);
            material.SetColor(PinnedId, style.pinned);
            material.SetColor(RetreatId, style.retreating);
            material.SetColor(HeavyCoverId, style.heavyCover);
            material.SetColor(LightCoverId, style.lightCover);
        }
    }
}
