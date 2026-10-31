using System;
using System.Collections.Generic;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Settled debris kept for the match and drawn from the GPU, up to <see cref="Cap"/> pieces. Past the cap the
    /// oldest piece sinks into the ground over <see cref="SinkTime"/> and its slot is reused.
    /// </summary>
    public sealed class LastingDebris : IDisposable
    {
        public const float SinkTime = 5f;

        /// <summary>Pieces that may sink at once; past it the oldest sinking piece goes at once.</summary>
        public const int SinkRoom = 512;

        private readonly LastingPiece[] _pieces;
        private readonly Queue<int> _standing;
        private readonly Queue<int> _sinking;
        private readonly Stack<int> _free;
        private readonly LastingDebrisDraws _draws;
        private float _clock;

        public LastingDebris(int cap)
        {
            Cap = cap;
            var capacity = cap == 0 ? 0 : cap + SinkRoom;
            _pieces = new LastingPiece[capacity];
            _standing = new Queue<int>(cap);
            _sinking = new Queue<int>(SinkRoom);
            _free = new Stack<int>(capacity);
            for (var slot = capacity - 1; slot >= 0; slot--)
            {
                _free.Push(slot);
            }

            _draws = new LastingDebrisDraws(capacity);
        }

        /// <summary>Pieces that lie for good; 0 keeps none.</summary>
        public int Cap { get; }

        public int Standing => _standing.Count;

        public int Sinking => _sinking.Count;

        public LastingPiece Piece(int slot) => _pieces[slot];

        /// <summary>Keeps <paramref name="piece"/>, thrown from <paramref name="set"/>, where it came to rest.</summary>
        public void Add(DebrisSet set, in DebrisPiece piece)
        {
            if (Standing == Cap)
            {
                StartSinking();
            }

            var slot = _free.Pop();
            _pieces[slot] = LastingPiece.Settled(piece);
            _standing.Enqueue(slot);
            _draws.Place(slot, set.meshes[piece.Mesh], set.material, _pieces[slot].Matrix(_clock));
        }

        /// <summary>Sinks the pieces past the cap and frees those fully under; pieces at rest cost nothing.</summary>
        public void Step(float deltaTime)
        {
            _clock += deltaTime;
            while (_sinking.Count > 0 && _pieces[_sinking.Peek()].Sunk(_clock))
            {
                Free(_sinking.Dequeue());
            }

            foreach (var slot in _sinking)
            {
                _draws.Move(slot, _pieces[slot].Matrix(_clock));
            }

            _draws.Upload();
        }

        public void Dispose() => _draws.Dispose();

        private void StartSinking()
        {
            if (Sinking == SinkRoom)
            {
                Free(_sinking.Dequeue());
            }

            var oldest = _standing.Dequeue();
            _pieces[oldest].Sinking = true;
            _pieces[oldest].SinkStart = _clock;
            _sinking.Enqueue(oldest);
        }

        private void Free(int slot)
        {
            _draws.Remove(slot);
            _free.Push(slot);
        }
    }
}
