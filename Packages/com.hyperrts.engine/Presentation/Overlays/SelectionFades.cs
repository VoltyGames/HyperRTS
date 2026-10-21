using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// When each selected unit was selected, so its marker settles in from a larger, faded start. Units drop out the
    /// frame they're deselected and animate again when picked once more.
    /// </summary>
    public sealed class SelectionFades
    {
        private readonly Dictionary<Entity, float> _since = new();
        private readonly HashSet<Entity> _seen = new();
        private readonly List<Entity> _gone = new();

        /// <summary>Marker scale and opacity at <paramref name="elapsed"/> seconds into a selection.</summary>
        public static Vector2 ScaleAndAlpha(float elapsed, float duration, float startScale)
        {
            var t = Mathf.Clamp01(elapsed / duration);
            var eased = 1f - (1f - t) * (1f - t);
            return new Vector2(Mathf.Lerp(startScale, 1f, eased), eased);
        }

        /// <summary>Seconds since <paramref name="entity"/> was selected, starting its clock on first sight.</summary>
        public float Elapsed(Entity entity, float now)
        {
            _seen.Add(entity);
            if (!_since.TryGetValue(entity, out var start))
            {
                start = now;
                _since.Add(entity, start);
            }

            return now - start;
        }

        /// <summary>Forgets units not asked about since the last call, so a later selection animates again.</summary>
        public void EndFrame()
        {
            foreach (var entity in _since.Keys)
            {
                if (!_seen.Contains(entity))
                {
                    _gone.Add(entity);
                }
            }

            foreach (var entity in _gone)
            {
                _since.Remove(entity);
            }

            _gone.Clear();
            _seen.Clear();
        }
    }
}
