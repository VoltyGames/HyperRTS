using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Stream state per entity and entry, kept only while the entity streams every frame: a stream that skipped a
    /// frame (hidden, out of range, gone) starts afresh instead of drawing a segment across the gap.
    /// </summary>
    public sealed class EffectStreams
    {
        private readonly Dictionary<(Entity, int), StreamState> _states = new();
        private readonly List<(Entity, int)> _stale = new();
        private readonly Stack<StreamState> _free = new();
        private int _frame;

        /// <summary>Stable per entity, so its stream keeps one look.</summary>
        public static uint Key(Entity entity, int index) => math.hash(new int3(entity.Index, entity.Version, index));

        /// <summary>Starts a frame; call <see cref="EndFrame"/> after the last <see cref="Step"/>.</summary>
        public void BeginFrame() => _frame++;

        /// <summary>The stream's state moved on to this frame's point.</summary>
        public StreamState Step(Entity entity, int index, Vector3 position)
        {
            var key = (entity, index);
            if (!_states.TryGetValue(key, out var state))
            {
                // Recycled: projectiles and reveals start streams every frame.
                state = _states[key] = _free.Count > 0 ? _free.Pop() : new StreamState();
                state.Frame = int.MinValue;
            }

            if (state.Frame != _frame - 1)
            {
                state.To = position;
                state.Carry = 0f;
            }

            state.From = state.To;
            state.To = position;
            state.Frame = _frame;
            return state;
        }

        /// <summary>Drops the streams not stepped this frame.</summary>
        public void EndFrame()
        {
            _stale.Clear();
            foreach (var (key, state) in _states)
            {
                if (state.Frame != _frame)
                {
                    _stale.Add(key);
                }
            }

            foreach (var key in _stale)
            {
                _states.Remove(key, out var state);
                _free.Push(state);
            }
        }
    }
}
