using HyperRTS.Simulation.Cues;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Impacts
{
    /// <summary>
    /// One mark kind's URP decal projectors, a projector per <see cref="ImpactMarkRing"/> slot, made as the ring first
    /// fills and reused after.
    /// </summary>
    public sealed class ImpactMarkDecals
    {
        /// <summary>Smallest fade change pushed to a projector; each push re-uploads the decal's data.</summary>
        private const float FadeStep = 1f / 64f;

        /// <summary>Faces steeper than these angles to the projection (unit sides, walls) fade out; map decals share them.</summary>
        public const float StartAngleFade = 50f;
        public const float EndAngleFade = 75f;

        private readonly ImpactMark _mark;
        private readonly Transform _root;
        private readonly DecalProjector[] _projectors;
        private readonly float[] _shown;

        public ImpactMarkDecals(ImpactMark mark, Transform root)
        {
            _mark = mark;
            _root = root;
            Ring = new ImpactMarkRing(mark.maxCount);
            _projectors = new DecalProjector[mark.maxCount];
            _shown = new float[mark.maxCount];
        }

        public ImpactMarkRing Ring { get; }

        /// <summary>Lays a fresh mark in the next slot; the projector is off while it changes, so it registers once.</summary>
        public void Place(float now, Material decal, Vector3 position, Quaternion rotation, float diameter, bool mirror)
        {
            var slot = Ring.Place(now);
            var projector = _projectors[slot];
            if (projector == null)
            {
                projector = _projectors[slot] = Create(slot);
            }

            projector.enabled = false;
            projector.material = decal;
            projector.size = new Vector3(diameter, diameter, _mark.depth);
            projector.uvScale = new Vector2(mirror ? -1f : 1f, 1f);
            projector.uvBias = new Vector2(mirror ? 1f : 0f, 0f);
            projector.fadeFactor = 1f;
            projector.transform.SetPositionAndRotation(position, rotation);
            projector.enabled = true;
            _shown[slot] = 1f;
        }

        /// <summary>Fades every mark by its age; a spent one is switched off until its slot is reused.</summary>
        public void Fade(float now)
        {
            for (var slot = 0; slot < Ring.Count; slot++)
            {
                var projector = _projectors[slot];
                if (!projector.enabled)
                {
                    continue;
                }

                var strength = ImpactMarkMath.Strength(Ring.Age(slot, now), _mark.lifetime, _mark.fadeTime);
                if (strength <= 0f)
                {
                    projector.enabled = false;
                }
                else if (Mathf.Abs(strength - _shown[slot]) >= FadeStep)
                {
                    projector.fadeFactor = strength;
                    _shown[slot] = strength;
                }
            }
        }

        public void Clear()
        {
            for (var slot = 0; slot < Ring.Count; slot++)
            {
                _projectors[slot].enabled = false;
            }

            Ring.Clear();
        }

        /// <summary>Built inactive and switched off, so it first registers with the decal system once placed.</summary>
        private DecalProjector Create(int slot)
        {
            var go = new GameObject($"{_mark.name} {slot}") { hideFlags = HideFlags.DontSave };
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            var projector = go.AddComponent<DecalProjector>();
            projector.enabled = false;
            projector.pivot = Vector3.zero;
            projector.startAngleFade = StartAngleFade;
            projector.endAngleFade = EndAngleFade;
            go.SetActive(true);
            return projector;
        }
    }
}
