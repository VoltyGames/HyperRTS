using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Common.Infantry;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>Resolving each figure's clips from its set and its own table when the squad is baked.</summary>
    public class SquadFigureAuthoringTests
    {
        private GameObject _root;
        private SquadFiguresAuthoring _squad;
        private FigureClipSet _set;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Squad");
            _squad = _root.AddComponent<SquadFiguresAuthoring>();
            _set = SquadFigureFixture.Set();
            _set.name = "Test Set";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var figure in _root.GetComponentsInChildren<BoneAnimatedAuthoring>())
            {
                Object.DestroyImmediate(figure.clips);
            }

            Object.DestroyImmediate(_set);
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void BuildClips_ResolvesTheSetFromEachFiguresOwnTable()
        {
            var first = AddFigure(0);
            var second = AddFigure(100);

            var near = _squad.BuildClips(0, first, _set);
            var far = _squad.BuildClips(1, second, _set);

            Assert.AreEqual(SquadFigureFixture.Range(20, true), near.Value.Lower(Posture.Stand, FigureState.Run));
            Assert.AreEqual(SquadFigureFixture.Range(120, true), far.Value.Lower(Posture.Stand, FigureState.Run));
            Assert.AreEqual(SquadFigureFixture.Range(260, false), far.Value.Fall(Posture.Prone, DeathCause.Shot, default, default));
            Assert.IsFalse(far.Value.Fire[(int)Posture.Stand].Loop, "a fire clip plays once per shot");
            near.Dispose();
            far.Dispose();

            second.GetComponentInChildren<BoneAnimatedAuthoring>().clips.clips.RemoveAt(4);
            var failure = Assert.Throws<KeyNotFoundException>(() => _squad.BuildClips(1, second, _set));
            StringAssert.Contains("Test Set", failure.Message);
            StringAssert.Contains("shotDeath Stand", failure.Message);
        }

        [Test]
        public void BuildClips_FailsOnAnUnsetEntryInsteadOfFallingBackToStanding()
        {
            var figure = AddFigure(0);

            _set.idle.crouch = "";
            var failure = Assert.Throws<InvalidOperationException>(() => _squad.BuildClips(0, figure, _set));
            StringAssert.Contains("idle Crouch", failure.Message);

            _set.idle.crouch = "CrouchIdle";
            _set.actions.Clear();
            Assert.Throws<InvalidOperationException>(() => _squad.BuildClips(0, figure, _set));

            Assert.Throws<InvalidOperationException>(() => _squad.BuildClips(0, figure, null));
        }

        // A figure with a body and a weapon sharing one table.
        private Transform AddFigure(int offset)
        {
            var table = SquadFigureFixture.Table(offset);
            var figure = new GameObject($"Figure {offset}");
            figure.transform.SetParent(_root.transform);
            foreach (var part in new[] { "Body", "Weapon" })
            {
                var child = new GameObject(part);
                child.transform.SetParent(figure.transform);
                child.AddComponent<BoneAnimatedAuthoring>().clips = table;
            }

            return figure.transform;
        }
    }
}
