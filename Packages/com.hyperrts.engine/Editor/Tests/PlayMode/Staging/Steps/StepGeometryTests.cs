using HyperRTS.Editor.PlayMode.Staging.Steps;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace HyperRTS.Editor.Tests.PlayMode.Staging.Steps
{
    /// <summary>The camera aims, view paths and spawn rows staging steps lay out, which need no Play.</summary>
    public class StepGeometryTests
    {
        [Test]
        public void ViewTurnedTowardTheSunKeepsItsShapeAndAddsTheHeading()
        {
            var aim = new CameraAim(new float2(3f, 4f), 2f, 10f, 8f, 30f).Turned(100f);

            Assert.That(aim.Yaw, Is.EqualTo(130f));
            Assert.That(aim.Distance, Is.EqualTo(10f));
            Assert.That(aim.Look, Is.EqualTo(new float2(3f, 4f)));
        }

        [Test]
        public void ViewEasesFromItsStartAimToItsEndAndHoldsThere()
        {
            var from = new CameraAim(new float2(0f, 0f), 2f, 10f, 0f, 0f);
            var to = new CameraAim(new float2(0f, 0f), 2f, 20f, 0f, 90f);
            var view = new ViewPath(from, to, 5f, 4f, _ => 1f, null);

            Assert.That(view.ViewAt(5f).position, Is.EqualTo(new Vector3(0f, 3f, -10f)).Using(Vector3EqualityComparer.Instance));
            Assert.That(view.ViewAt(7f).position, Is.EqualTo(new Vector3(-15f * math.SQRT2 / 2f, 3f, -15f * math.SQRT2 / 2f)).Using(Vector3EqualityComparer.Instance));
            Assert.That(view.ViewAt(20f).position, Is.EqualTo(new Vector3(-20f, 3f, 0f)).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void SpawnRowStandsAbreastAcrossTheFacingAroundItsCentre()
        {
            var row = new SpawnRow(new float2(100f, 50f), 90f, 8f, 3);

            Assert.That(row.Position(0).x, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(row.Position(0).y, Is.EqualTo(58f).Within(1e-3f));
            Assert.That(row.Position(1).y, Is.EqualTo(50f).Within(1e-3f));
            Assert.That(row.Position(2).y, Is.EqualTo(42f).Within(1e-3f));
        }
    }
}
