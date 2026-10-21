using System.Collections.Generic;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Client debris: the bounce integrator, the global cap with its reuse of old pieces, and the quality gate.</summary>
    public class DebrisTests
    {
        private const float Frame = 1f / 60f;

        private DebrisSet _set;

        [SetUp]
        public void SetUp()
        {
            _set = ScriptableObject.CreateInstance<DebrisSet>();
            _set.meshes = new List<Mesh> { new() };
            _set.material = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_set.meshes[0]);
            Object.DestroyImmediate(_set.material);
            Object.DestroyImmediate(_set);
        }

        [Test]
        public void ThrownPiece_BouncesOffTheGround_ThenLiesFlatOnIt()
        {
            var piece = new DebrisPiece
            {
                Position = new float3(0f, 2f, 0f),
                Velocity = new float3(2f, 5f, 0f),
                Rotation = quaternion.Euler(0.4f, 1f, 0.7f),
                Spin = new float3(0f, 0f, 6f),
                Size = new float3(1.2f, 0.1f, 0.15f),
                HalfHeight = 0.05f,
                Life = 15f,
            };
            var bounced = false;
            var rising = false;
            for (var i = 0; i < 600 && !piece.Resting; i++)
            {
                var falling = piece.Velocity.y < 0f;
                piece.Step(Frame, 0.5f);
                rising = falling && piece.Velocity.y > 0f;
                bounced |= rising;
            }

            Assert.IsTrue(bounced, "bounces at least once");
            Assert.IsTrue(piece.Resting);
            Assert.AreEqual(0.55f, piece.Position.y, 1e-4f, "half its height above the ground");
            var up = math.rotate(piece.Rotation, math.up());
            Assert.AreEqual(1f, up.y, 1e-4f, "lies flat");
        }

        [Test]
        public void Piece_ShrinksAway_AtTheEndOfItsLife()
        {
            var piece = new DebrisPiece { Life = 12f, Resting = true };

            piece.Step(11f, 0f);
            Assert.AreEqual(0.5f, piece.Shrink, 1e-4f);
            Assert.IsFalse(piece.Gone);

            piece.Step(1f, 0f);
            Assert.IsTrue(piece.Gone);
        }

        [Test]
        public void FullField_ReusesItsOldestPieces_AndNeverPassesTheCap()
        {
            var field = new DebrisField();
            field.Throw(_set, float3.zero, float3.zero, 1f, 10, 7u);
            field.Step(5f, default);
            field.Throw(_set, new float3(20f, 0f, 0f), float3.zero, 1f, DebrisField.Capacity, 9u);

            Assert.AreEqual(DebrisField.Capacity, field.Count);
            for (var slot = 0; slot < DebrisField.Capacity; slot++)
            {
                Assert.Greater(field.Piece(slot).Position.x, 15f, "the first throw's pieces were reused");
            }
        }

        [Test]
        public void Pieces_FreeTheirSlots_WhenGone()
        {
            var field = new DebrisField();
            field.Throw(_set, float3.zero, float3.zero, 1f, 6, 3u);

            for (var i = 0; i < 30; i++)
            {
                field.Step(1f, default);
            }

            Assert.AreEqual(0, field.Count);
        }

        [Test]
        public void SameSeed_ThrowsTheSamePieces()
        {
            var first = new DebrisField();
            var second = new DebrisField();

            first.Throw(_set, new float3(4f, 0f, 2f), math.right(), 1f, 5, 42u);
            second.Throw(_set, new float3(4f, 0f, 2f), math.right(), 1f, 5, 42u);

            for (var slot = 0; slot < 5; slot++)
            {
                Assert.AreEqual(first.Piece(slot).Velocity, second.Piece(slot).Velocity);
            }
        }

        [Test]
        public void LowEffects_ThrowNothing_AndMediumFewer()
        {
            Assert.AreEqual(0, DebrisSystem.PieceCount(8, DetailLevel.Low));
            Assert.Less(DebrisSystem.PieceCount(8, DetailLevel.Medium), 8);
            Assert.AreEqual(8, DebrisSystem.PieceCount(8, DetailLevel.High));
        }
    }
}
