using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Every thrown <see cref="DebrisPiece"/> on this client, in a fixed pool: a full pool reuses its oldest piece.
    /// Pieces from one seed fly the same on every client.
    /// </summary>
    public sealed class DebrisField
    {
        public const int Capacity = 160;

        // Launches lean outward this much against straight up.
        private const float Outward = 0.6f;
        private const float MaxSpin = 9f;
        private const float StartHeight = 0.4f;

        private readonly DebrisPiece[] _pieces = new DebrisPiece[Capacity];
        private readonly DebrisSet[] _sets = new DebrisSet[Capacity];

        // Reused each frame so drawing allocates nothing.
        private readonly DebrisInstance[] _batch = new DebrisInstance[Capacity];
        private readonly Matrix4x4[] _previous = new Matrix4x4[Capacity];
        private readonly bool[] _drawn = new bool[Capacity];

        public int Count { get; private set; }

        public DebrisPiece Piece(int slot) => _pieces[slot];

        /// <summary>Throws <paramref name="count"/> pieces of <paramref name="set"/> up and toward <paramref name="outward"/>.</summary>
        public void Throw(DebrisSet set, float3 center, float3 outward, float radius, int count, uint seed)
        {
            var random = new Unity.Mathematics.Random(math.max(seed, 1u));
            for (var i = 0; i < count; i++)
            {
                var slot = FreeSlot();
                Count += _sets[slot] == null ? 1 : 0;
                _sets[slot] = set;
                _pieces[slot] = Launch(set, center, outward, radius, ref random);
                _previous[slot] = _pieces[slot].Matrix;
            }
        }

        public void Step(float deltaTime, in TerrainHeight terrain)
        {
            for (var slot = 0; slot < Capacity; slot++)
            {
                if (_sets[slot] == null)
                {
                    continue;
                }

                _pieces[slot].Step(deltaTime, terrain.Height(_pieces[slot].Position.xz));
                if (_pieces[slot].Gone)
                {
                    _sets[slot] = null;
                    Count--;
                }
            }
        }

        /// <summary>Draws every piece, one instanced call per material and mesh pair.</summary>
        public void Draw()
        {
            System.Array.Clear(_drawn, 0, Capacity);
            for (var slot = 0; slot < Capacity; slot++)
            {
                if (_sets[slot] == null || _drawn[slot])
                {
                    continue;
                }

                var material = _sets[slot].material;
                var mesh = PieceMesh(slot);
                var count = 0;
                for (var other = slot; other < Capacity; other++)
                {
                    var sameBatch = _sets[other] != null && _sets[other].material == material && PieceMesh(other) == mesh;
                    if (sameBatch)
                    {
                        var matrix = _pieces[other].Matrix;
                        _batch[count++] = new DebrisInstance { objectToWorld = matrix, prevObjectToWorld = _previous[other] };
                        _previous[other] = matrix;
                        _drawn[other] = true;
                    }
                }

                var draw = new RenderParams(material)
                {
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    motionVectorMode = MotionVectorGenerationMode.Object,
                };
                Graphics.RenderMeshInstanced(draw, mesh, 0, _batch, count);
            }
        }

        private Mesh PieceMesh(int slot) => _sets[slot].meshes[_pieces[slot].Mesh];

        // An empty slot, else the piece that has lain longest.
        private int FreeSlot()
        {
            var oldest = 0;
            for (var slot = 0; slot < Capacity; slot++)
            {
                if (_sets[slot] == null)
                {
                    return slot;
                }

                oldest = _pieces[slot].Age > _pieces[oldest].Age ? slot : oldest;
            }

            return oldest;
        }

        private static DebrisPiece Launch(DebrisSet set, float3 center, float3 outward, float radius, ref Unity.Mathematics.Random random)
        {
            var around = random.NextFloat2Direction();
            var spread = new float3(around.x, 0f, around.y);
            var direction = math.normalize(math.up() + (spread + outward) * Outward);
            var mesh = random.NextInt(set.meshes.Count);
            var size = (float3)set.size * random.NextFloat(0.8f, 1.2f);
            return new DebrisPiece
            {
                Position = center + spread * (radius * random.NextFloat()) + math.up() * StartHeight,
                Velocity = direction * random.NextFloat(set.speed.x, set.speed.y),
                Spin = random.NextFloat3Direction() * random.NextFloat(MaxSpin),
                Rotation = random.NextQuaternionRotation(),
                Size = size,
                HalfHeight = set.meshes[mesh].bounds.extents.y * size.y,
                Mesh = mesh,
                Life = random.NextFloat(set.life.x, set.life.y),
            };
        }
    }
}
