using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.HUD.Map
{
    /// <summary>Draws the markers in order into a transparent pixel grid: a fill with a centred outline, soft at the edge.</summary>
    [BurstCompile]
    public struct MinimapMarkerJob : IJob
    {
        [ReadOnly] public NativeArray<MinimapMarker> Markers;
        public NativeArray<Color32> Pixels;
        public int2 Size;
        public Color32 Outline;
        public float OutlineWidth;

        public void Execute()
        {
            for (var i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = default;
            }

            var half = OutlineWidth * 0.5f;
            foreach (var marker in Markers)
            {
                // Texture rows count up from the bottom, markers count down from the top.
                var center = new float2(marker.Point.x * Size.x, (1f - marker.Point.y) * Size.y);
                var reach = (int)math.ceil(marker.Radius + half + 1f);
                var low = math.max((int2)math.floor(center) - reach, 0);
                var high = math.min((int2)math.ceil(center) + reach, Size - 1);
                for (var y = low.y; y <= high.y; y++)
                {
                    for (var x = low.x; x <= high.x; x++)
                    {
                        Blend(y * Size.x + x, marker, new float2(x + 0.5f, y + 0.5f) - center, half);
                    }
                }
            }
        }

        private void Blend(int index, in MinimapMarker marker, float2 offset, float half)
        {
            var distance = marker.Square ? math.cmax(math.abs(offset)) : math.length(offset);
            var outer = math.saturate(marker.Radius + half + 0.5f - distance);
            if (outer <= 0f)
            {
                return;
            }

            var inner = math.saturate(marker.Radius - half + 0.5f - distance);
            var ink = math.lerp(ToFloat(Outline), ToFloat(marker.Color), inner);
            var alpha = outer * ink.w;
            if (alpha <= 0f)
            {
                return;
            }

            var below = ToFloat(Pixels[index]);
            var coverage = alpha + below.w * (1f - alpha);
            var rgb = (ink.xyz * alpha + below.xyz * below.w * (1f - alpha)) / coverage;
            var packed = (int4)(new float4(rgb, coverage) * 255f + 0.5f);
            Pixels[index] = new Color32((byte)packed.x, (byte)packed.y, (byte)packed.z, (byte)packed.w);
        }

        private static float4 ToFloat(Color32 color) => new float4(color.r, color.g, color.b, color.a) / 255f;
    }
}
