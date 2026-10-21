using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Cursors;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Which cursor, size and frame a cursor set shows.</summary>
    public class CursorSetTests
    {
        private CursorSet _set;

        [SetUp]
        public void SetUp()
        {
            _set = ScriptableObject.CreateInstance<CursorSet>();
            _set.animations = new[] { Animation(CursorKind.Default, true), Animation(CursorKind.Select, false) };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_set);

        [Test]
        public void UnmappedKind_FallsBackToDefault()
        {
            Assert.AreEqual(CursorKind.Select, _set.Animation(CursorKind.Select).kind);
            Assert.AreEqual(CursorKind.Default, _set.Animation(CursorKind.Attack).kind);
        }

        [Test]
        public void SetWithoutDefault_Throws()
        {
            _set.animations = new[] { Animation(CursorKind.Select, true) };

            Assert.Throws<MissingReferenceException>(() => _set.Animation(CursorKind.Attack));
        }

        [Test]
        public void LoopingCursor_Wraps_AndOneShotHoldsItsLastFrame()
        {
            Assert.AreEqual(2, _set.Animation(CursorKind.Default).FrameAt(1.25f, 4));
            Assert.AreEqual(3, _set.Animation(CursorKind.Select).FrameAt(1.25f, 4));
        }

        [TestCase(1f, 32)]
        [TestCase(1.5f, 48)]
        [TestCase(2f, 64)]
        [TestCase(3f, 64)]
        [TestCase(1.25f, 48)]
        public void Scale_PicksTheClosestDrawnSize(float scale, int expected) =>
            Assert.AreEqual(expected, _set.Animation(CursorKind.Default).Closest(_set.baseSize * scale).size);

        private static CursorAnimation Animation(CursorKind kind, bool loop) => new()
        {
            kind = kind,
            fps = 8f,
            loop = loop,
            sizes = new[] { Sized(32), Sized(48), Sized(64) },
        };

        private static CursorFrames Sized(int size) => new() { size = size, frames = new Texture2D[4] };
    }
}
