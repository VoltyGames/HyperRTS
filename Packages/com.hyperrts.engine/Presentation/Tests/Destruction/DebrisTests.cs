using System.Collections.Generic;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>
    /// Client debris: the bounce integrator, the flying pool's reuse of old pieces, the lasting layer with its cap and
    /// sinking of the oldest, the instancing check and the quality gate.
    /// </summary>
    public class DebrisTests
    {
        private const float Frame = 1f / 60f;

        private DebrisSet _set;
        private LastingDebris _lasting;

        [SetUp]
        public void SetUp()
        {
            _set = ScriptableObject.CreateInstance<DebrisSet>();
            var triangle = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward }, triangles = new[] { 0, 1, 2 } };
            _set.meshes = new List<Mesh> { triangle };
            _set.material = new Material(Shader.Find("Hidden/InternalErrorShader")) { enableInstancing = true };
        }

        [TearDown]
        public void TearDown()
        {
            _lasting?.Dispose();
            _lasting = null;
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
        public void FullField_ReusesItsOldestPieces_AndNeverPassesTheCap()
        {
            _lasting = new LastingDebris(0);
            var field = new DebrisField();
            field.Throw(_set, float3.zero, float3.zero, 1f, 10, 7u);
            field.Step(5f, default, _lasting);
            field.Throw(_set, new float3(20f, 0f, 0f), float3.zero, 1f, DebrisField.Capacity, 9u);

            Assert.AreEqual(DebrisField.Capacity, field.Count);
            for (var slot = 0; slot < DebrisField.Capacity; slot++)
            {
                Assert.Greater(field.Piece(slot).Position.x, 15f, "the first throw's pieces were reused");
            }
        }

        [Test]
        public void SettledPieces_MoveIntoTheLastingLayer()
        {
            _lasting = new LastingDebris(EffectDetail.LastingDebrisAt(DetailLevel.High));
            var field = new DebrisField();
            field.Throw(_set, float3.zero, float3.zero, 1f, 6, 3u);

            for (var i = 0; i < 600; i++)
            {
                field.Step(Frame, default, _lasting);
            }

            Assert.AreEqual(0, field.Count, "no piece is left flying");
            Assert.AreEqual(6, _lasting.Standing);
        }

        [Test]
        public void FullLastingLayer_SinksItsOldest_ThenReusesItsSlot()
        {
            _lasting = new LastingDebris(2);
            for (var x = 0; x < 3; x++)
            {
                _lasting.Add(_set, Settled(x));
            }

            Assert.AreEqual(2, _lasting.Standing, "the cap holds");
            Assert.AreEqual(1, _lasting.Sinking);
            Assert.IsTrue(_lasting.Piece(0).Sinking, "the oldest sinks");

            _lasting.Step(LastingDebris.SinkTime);
            Assert.AreEqual(0, _lasting.Sinking, "a sunk piece frees its slot");

            _lasting.Add(_set, Settled(3));
            Assert.AreEqual(3f, _lasting.Piece(0).Position.x, "the sunk piece's slot is reused");
            Assert.AreEqual(2, _lasting.Standing);
        }

        [Test]
        public void SinkingPiece_GoesUnderTheGround_OverTheSinkTime()
        {
            var piece = new LastingPiece { Rotation = quaternion.identity, Size = 1f, Depth = 0.4f, Sinking = true };

            Assert.AreEqual(-0.2f, piece.Matrix(LastingDebris.SinkTime / 2f).c3.y, 1e-4f);
            Assert.IsFalse(piece.Sunk(LastingDebris.SinkTime / 2f));
            Assert.IsTrue(piece.Sunk(LastingDebris.SinkTime));
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
        public void Set_WithoutInstancingOnItsMaterial_HasAProblem()
        {
            Assert.IsNull(_set.Problem());

            _set.material.enableInstancing = false;
            StringAssert.Contains("Instancing", _set.Problem());
        }

        [Test]
        public void LowEffects_ThrowAndKeepNothing_AndMediumFewer()
        {
            Assert.AreEqual(0, DebrisSystem.PieceCount(8, DetailLevel.Low));
            Assert.Less(DebrisSystem.PieceCount(8, DetailLevel.Medium), 8);
            Assert.AreEqual(8, DebrisSystem.PieceCount(8, DetailLevel.High));
            Assert.AreEqual(0, EffectDetail.LastingDebrisAt(DetailLevel.Low));
            Assert.AreEqual(8000, EffectDetail.LastingDebrisAt(DetailLevel.Medium));
            Assert.AreEqual(20000, EffectDetail.LastingDebrisAt(DetailLevel.High));
        }

        [Test]
        public void NoLastingLayer_DropsSettledPieces()
        {
            _lasting = new LastingDebris(EffectDetail.LastingDebrisAt(DetailLevel.Low));
            var field = new DebrisField();
            field.Throw(_set, float3.zero, float3.zero, 1f, 4, 5u);

            for (var i = 0; i < 600; i++)
            {
                field.Step(Frame, default, _lasting);
            }

            Assert.AreEqual(0, field.Count);
            Assert.AreEqual(0, _lasting.Standing);
        }

        private static DebrisPiece Settled(float x) => new()
        {
            Position = new float3(x, 0.1f, 0f),
            Rotation = quaternion.identity,
            Size = 1f,
            HalfHeight = 0.1f,
            Resting = true,
        };
    }
}
