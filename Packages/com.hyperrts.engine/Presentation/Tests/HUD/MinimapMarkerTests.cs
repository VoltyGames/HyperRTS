using HyperRTS.Presentation.HUD.Map;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>The minimap's marker job: where a blip lands in the texture and which one wins an overlap.</summary>
    public class MinimapMarkerTests
    {
        private static readonly Color32 Red = new(255, 0, 0, 255);
        private static readonly Color32 Blue = new(0, 0, 255, 255);

        [Test]
        public void Job_FillsTheMarker_LeavesTheRestClear_AndLetsTheLaterMarkerCover()
        {
            const int Side = 20;
            using var markers = new NativeArray<MinimapMarker>(new[]
            {
                new MinimapMarker { Point = new float2(0.5f, 0.5f), Color = Red, Radius = 6f, Square = true },
                new MinimapMarker { Point = new float2(0.5f, 0.5f), Color = Blue, Radius = 2f, Square = false },
            }, Allocator.TempJob);
            using var pixels = new NativeArray<Color32>(Side * Side, Allocator.TempJob);

            new MinimapMarkerJob
            {
                Markers = markers, Pixels = pixels, Size = new int2(Side, Side), Outline = new Color32(0, 0, 0, 255),
                OutlineWidth = 1f,
            }.Run();

            Assert.AreEqual(Blue, pixels[10 * Side + 10], "the later, smaller marker covers the middle");
            Assert.AreEqual(Red, pixels[10 * Side + 13], "the square's fill outside the circle");
            Assert.AreEqual(0, pixels[0].a, "far from any marker");
        }

        [Test]
        public void Job_PutsTheTopOfTheMinimapAtTheTopRow()
        {
            const int Side = 20;
            using var markers = new NativeArray<MinimapMarker>(new[]
            {
                new MinimapMarker { Point = new float2(0.5f, 0.1f), Color = Red, Radius = 1f, Square = true },
            }, Allocator.TempJob);
            using var pixels = new NativeArray<Color32>(Side * Side, Allocator.TempJob);

            new MinimapMarkerJob
            {
                Markers = markers, Pixels = pixels, Size = new int2(Side, Side), OutlineWidth = 0f,
            }.Run();

            Assert.AreEqual(255, pixels[18 * Side + 10].a, "UI y counts down, texture rows count up");
            Assert.AreEqual(0, pixels[2 * Side + 10].a);
        }
    }
}
