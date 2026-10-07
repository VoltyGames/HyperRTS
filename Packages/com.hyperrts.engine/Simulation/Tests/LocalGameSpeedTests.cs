using HyperRTS.Simulation.Match;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Pause keeps the chosen speed, and reset leaves a match at normal speed.</summary>
    public class LocalGameSpeedTests
    {
        [TearDown]
        public void TearDown() => LocalGameSpeed.Reset();

        [Test]
        public void Pause_StopsTime_AndResumeRestoresTheChosenSpeed()
        {
            LocalGameSpeed.SetScale(2f);
            LocalGameSpeed.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(LocalGameSpeed.IsPaused);

            LocalGameSpeed.SetScale(1.5f);
            Assert.AreEqual(0f, Time.timeScale, "a speed change while paused waits for resume");

            LocalGameSpeed.Resume();
            Assert.AreEqual(1.5f, Time.timeScale);
        }

        [Test]
        public void Reset_UnpausesAtNormalSpeed()
        {
            LocalGameSpeed.SetScale(4f);
            LocalGameSpeed.Pause();

            LocalGameSpeed.Reset();

            Assert.IsFalse(LocalGameSpeed.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
