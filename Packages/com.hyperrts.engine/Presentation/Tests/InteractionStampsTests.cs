using System.Collections.Generic;
using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class InteractionStampsTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private ImpactMark Mark(float craterDepth, float scorch)
        {
            var mark = ScriptableObject.CreateInstance<ImpactMark>();
            _created.Add(mark);
            mark.craterDepth = craterDepth;
            mark.scorch = scorch;
            return mark;
        }

        [Test]
        public void AddCrater_BowlIsHalfTheMarkInsideAWiderScorchDisc()
        {
            var mark = Mark(0.8f, 0.6f);
            var stamps = new InteractionStamps();

            stamps.AddCrater(new float2(1f, 2f), mark, 4f);

            var scar = stamps.Scars[0];
            Assert.AreEqual(new float2(2f * InteractionMath.ScorchReach), scar.HalfSize);
            Assert.AreEqual(0.8f / InteractionMath.MaxCraterDepth, scar.Value.x, 1e-5f);
            Assert.AreEqual(0.6f, scar.Value.y, 1e-5f);
            Assert.AreEqual(1f / InteractionMath.ScorchReach, scar.Value.z, 1e-5f, "bowl share of the stamp");
            Assert.AreEqual(scar.HalfSize, stamps.Trample[0].HalfSize, "plants flattened over the whole scar");
            Assert.AreEqual(1f, stamps.Trample[0].Value.w, "round");
            Assert.IsEmpty(stamps.Tracks, "scars stay out of the fading channels' stamps");
        }

        [Test]
        public void AddCrater_WithoutScorch_IsJustTheBowl_AndDeepCratersClip()
        {
            var mark = Mark(5f, 0f);
            var stamps = new InteractionStamps();

            stamps.AddCrater(float2.zero, mark, 6f);

            Assert.AreEqual(new float2(3f), stamps.Scars[0].HalfSize);
            Assert.AreEqual(new float4(1f, 0f, 1f, 1f), stamps.Scars[0].Value);
        }

        [Test]
        public void AddWake_TrailsBehindTheUnit_FoamGrowingWithSpeed()
        {
            var stamps = new InteractionStamps();

            stamps.AddWake(new float2(10f, 0f), new float2(1f, 0f), 4f, 2f);
            stamps.AddWake(float2.zero, new float2(0f, 1f), 2f * InteractionMath.FullWakeSpeed, 2f);

            var wake = stamps.Wakes[0];
            Assert.Less(wake.Center.x, 10f, "centre behind the unit");
            Assert.AreEqual(10f + 2f, wake.Center.x + wake.HalfSize.y, 1e-4f, "front edge at the unit's bow");
            Assert.AreEqual(4f / InteractionMath.FullWakeSpeed, wake.Value.x, 1e-5f);
            Assert.AreEqual(1f, stamps.Wakes[1].Value.x, "full foam caps");
        }

        [Test]
        public void Clear_EmptiesEveryListAndTheReset()
        {
            var stamps = new InteractionStamps { Reset = true };
            stamps.AddWake(float2.zero, new float2(0f, 1f), 3f, 1f);

            stamps.Clear();

            Assert.IsEmpty(stamps.Wakes);
            Assert.IsFalse(stamps.Reset);
        }

        [Test]
        public void ScarsGround_OnlyWithACraterOrScorch()
        {
            var none = Mark(0f, 0f);
            var scorch = Mark(0f, 0.9f);

            Assert.IsFalse(none.ScarsGround);
            Assert.IsTrue(scorch.ScarsGround);
        }
    }
}
