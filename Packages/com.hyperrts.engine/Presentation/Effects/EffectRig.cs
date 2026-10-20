using HyperRTS.Presentation.Common;
using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Root of an effect rig prefab: child particle systems in world space, played by hand from a pool. Bursts
    /// replay the authored bursts at a pose; streams spread the authored rates along a path. Author it +Y up, +Z forward.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Effect Rig")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public partial class EffectRig : PresentationBehaviour
    {
        [Tooltip("Not played beyond this many metres from the camera.")]
        public float maxDistance = 150f;

        [Tooltip("Random scale range of each burst.")]
        public Vector2 scale = Vector2.one;

        [Tooltip("Scale added per metre of an impact's splash radius.")]
        public float scalePerRadius;

        [Tooltip("Impacts face the ground normal instead of straight up.")]
        public bool alignToGround = true;

        [Tooltip("Turns each burst, and each entity's loop, by a random angle about the rig's +Y. Off keeps aim.")]
        public bool randomSpin;

        [Tooltip("Alternative rigs, each without variants of its own. Each play picks this rig or one of them.")]
        public GameObject[] variants = Array.Empty<GameObject>();

        private readonly List<DelayedBurst> _delayed = new();
        private EffectEmitter[] _emitters;
        private EffectLight[] _lights;
        private readonly StreamShares _shares = new();

        /// <summary>The shared flash lights; none means no flashes.</summary>
        public EffectLights Lights { get; set; }

        /// <summary>Set by the pool from <see cref="variants"/>.</summary>
        public EffectRig[] Alternatives { get; set; } = Array.Empty<EffectRig>();

        /// <summary>Reads the authored emission now, so a broken rig fails when the pool first loads it.</summary>
        public void Prepare() => Captured();

        /// <summary>A random burst scale grown by an impact's splash radius.</summary>
        public float ScaleFor(float splashRadius) =>
            UnityEngine.Random.Range(scale.x, scale.y) + scalePerRadius * splashRadius;

        /// <summary>Bursts a random variant at a random scale from its range, grown by an impact's splash radius.</summary>
        public void Play(Vector3 position, Quaternion rotation, float splashRadius = 0f)
        {
            var played = Pick();
            played.Burst(position, rotation, played.ScaleFor(splashRadius), EffectDetail.Density);
        }

        public EffectRig Pick(uint key)
        {
            var pick = (int)(key % (uint)(Alternatives.Length + 1));
            return pick == 0 ? this : Alternatives[pick - 1];
        }

        public EffectRig Pick() => Pick((uint)UnityEngine.Random.Range(0, int.MaxValue));

        public float LoopScale(uint key) => Mathf.Lerp(scale.x, scale.y, Fraction(key, 1u));

        public Quaternion LoopRotation(Quaternion rotation, uint key) =>
            randomSpin ? rotation * Yaw(Fraction(key, 2u)) : rotation;

        private static float Fraction(uint key, uint salt) => (math.hash(new uint2(key, salt)) >> 8) / 16777216f;

        /// <summary>Fires every authored burst at the pose, the timed ones later, thinned by density.</summary>
        public void Burst(Vector3 position, Quaternion rotation, float burstScale, float density)
        {
            var emitters = Captured();
            if (randomSpin)
            {
                rotation *= Yaw(UnityEngine.Random.value);
            }

            Pose(position, rotation, burstScale);
            for (var e = 0; e < emitters.Length; e++)
            {
                for (var b = 0; b < emitters[e].BurstCount; b++)
                {
                    for (var cycle = 0; cycle < emitters[e].BurstCycles(b); cycle++)
                    {
                        var count = emitters[e].BurstSize(b, density);
                        Fire(e, count, emitters[e].BurstTime(b, cycle), position, rotation, burstScale);
                    }
                }
            }

            Flash();
        }

        private void Fire(int emitter, int count, float time, Vector3 position, Quaternion rotation, float burstScale)
        {
            if (count <= 0)
            {
                return;
            }

            if (time <= 0f)
            {
                EmitPosed(_emitters[emitter], count);
                return;
            }

            _delayed.Add(new DelayedBurst
            {
                Remaining = time, Emitter = emitter, Count = count,
                Position = position, Rotation = rotation, Scale = burstScale,
            });
        }

        private static Quaternion Yaw(float fraction) => Quaternion.Euler(0f, fraction * 360f, 0f);

        /// <summary>
        /// Emits the authored rates for <paramref name="deltaTime"/> and the distance from→to, scaled by strength and
        /// density, spread evenly along the path. <paramref name="carry"/> keeps the fraction of a particle per stream.
        /// </summary>
        public void Stream(Vector3 from, Vector3 to, Quaternion rotation, float deltaTime, float strength,
            float density, ref float carry, float poseScale = 1f)
        {
            var emitters = Captured();
            var counts = _shares.Split(emitters, deltaTime, Vector3.Distance(from, to), strength * density, ref carry);
            for (var e = 0; e < emitters.Length; e++)
            {
                Spread(emitters[e], counts[e], from, to, rotation, poseScale);
            }
        }

        /// <summary>Fires the queued timed bursts that are due.</summary>
        public void Tick(float deltaTime)
        {
            for (var i = _delayed.Count - 1; i >= 0; i--)
            {
                var burst = _delayed[i];
                burst.Remaining -= deltaTime;
                if (burst.Remaining > 0f)
                {
                    _delayed[i] = burst;
                    continue;
                }

                _delayed.RemoveAt(i);
                Pose(burst.Position, burst.Rotation, burst.Scale);
                EmitPosed(_emitters[burst.Emitter], burst.Count);
            }
        }

        private void Spread(EffectEmitter emitter, int count, Vector3 from, Vector3 to, Quaternion rotation,
            float poseScale)
        {
            for (var i = 1; i <= count; i++)
            {
                Pose(Vector3.Lerp(from, to, i / (float)count), rotation, poseScale);
                EmitPosed(emitter, 1);
            }
        }

        // Live particles render at the rig's current scale, so it returns to 1 once the posed ones carry it.
        private void EmitPosed(EffectEmitter emitter, int count)
        {
            emitter.Emit(count, transform.localScale.x);
            transform.localScale = Vector3.one;
        }

        private void Pose(Vector3 position, Quaternion rotation, float poseScale)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = Vector3.one * poseScale;
        }

        private void Flash()
        {
            if (Lights == null || EffectDetail.Level != DetailLevel.High)
            {
                return;
            }

            foreach (var light in _lights)
            {
                Lights.Flash(light, light.transform.position);
            }
        }

        /// <summary>Reads the authored emission on first use.</summary>
        private EffectEmitter[] Captured()
        {
            if (_emitters != null)
            {
                return _emitters;
            }

            var systems = GetComponentsInChildren<ParticleSystem>(true);
            var subEmitters = SubEmitters(systems);
            var emitters = new List<EffectEmitter>();
            foreach (var system in systems)
            {
                if (!subEmitters.Contains(system))
                {
                    emitters.Add(new EffectEmitter(system));
                }
            }

            _lights = GetComponentsInChildren<EffectLight>(true);
            return _emitters = emitters.ToArray();
        }

        /// <summary>Sub-emitters keep their own emission: their parent triggers them.</summary>
        private static HashSet<ParticleSystem> SubEmitters(ParticleSystem[] systems)
        {
            var found = new HashSet<ParticleSystem>();
            foreach (var system in systems)
            {
                var subs = system.subEmitters;
                for (var i = 0; subs.enabled && i < subs.subEmittersCount; i++)
                {
                    found.Add(subs.GetSubEmitterSystem(i));
                }
            }

            return found;
        }
    }
}
