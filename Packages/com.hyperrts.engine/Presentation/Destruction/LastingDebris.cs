using HyperRTS.Simulation.Common;
using System;
using System.Collections.Generic;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Settled debris kept for the match and drawn from the GPU, up to <see cref="Cap"/> pieces. Past the cap the
    /// oldest piece buries into the ground over <see cref="BuryTime"/> and its slot is reused.
    /// </summary>
    public sealed class LastingDebris : IDisposable
    {
        public const float BuryTime = 5f;

        /// <summary>Pieces that may bury at once; past it the oldest burying piece goes at once.</summary>
        public const int BuryRoom = 512;

        private readonly LastingPiece[] _pieces;
        private readonly Queue<int> _standing;
        private readonly Queue<int> _burying;
        private readonly Stack<int> _free;
        private readonly LastingDebrisDraws _draws;
        private float _clock;

        /// <summary>Settled pieces kept for the match at <paramref name="level"/>.</summary>
        public static int CapAt(DetailLevel level) => level switch
        {
            DetailLevel.Low => 0,
            DetailLevel.Medium => 8000,
            _ => 20000,
        };

        public LastingDebris(int cap)
        {
            Cap = cap;
            var capacity = cap == 0 ? 0 : cap + BuryRoom;
            _pieces = new LastingPiece[capacity];
            _standing = new Queue<int>(cap);
            _burying = new Queue<int>(BuryRoom);
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

        public int Burying => _burying.Count;

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
            while (_burying.Count > 0 && _pieces[_burying.Peek()].Buried(_clock))
            {
                Free(_burying.Dequeue());
            }

            foreach (var slot in _burying)
            {
                _draws.Move(slot, _pieces[slot].Matrix(_clock));
            }

            _draws.Upload();
        }

        public void Dispose() => _draws.Dispose();

        private void StartSinking()
        {
            if (Burying == BuryRoom)
            {
                Free(_burying.Dequeue());
            }

            var oldest = _standing.Dequeue();
            _pieces[oldest].Burying = true;
            _pieces[oldest].BuryStart = _clock;
            _burying.Enqueue(oldest);
        }

        private void Free(int slot)
        {
            _draws.Remove(slot);
            _free.Push(slot);
        }
    }
}
