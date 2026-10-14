using System;
using System.Collections.Generic;
using HyperRTS.Core;
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
    public class EffectRig : MonoBehaviour
    {
        [Tooltip("Not played beyond this many metres from the camera.")]
        public float maxDistance = 150f;

        [Tooltip("Random scale range of each burst.")]
        public Vector2 scale = Vector2.one;

        [Tooltip("Scale added per metre of an impact's splash radius.")]
        public float scalePerRadius;

        [Tooltip("Impacts face the ground normal instead of straight up.")]
        public bool alignToGround = true;

        private readonly List<DelayedBurst> _delayed = new();
        private EffectEmitter[] _emitters;
        private EffectLight[] _lights;
        private int[] _counts;

        /// <summary>The shared flash lights; none means no flashes.</summary>
        public EffectLights Lights { get; set; }

        /// <summary>Reads the authored emission now, so a broken rig fails when the pool first loads it.</summary>
        public void Prepare() => Captured();

        /// <summary>A random burst scale grown by an impact's splash radius.</summary>
        public float ScaleFor(float splashRadius) =>
            UnityEngine.Random.Range(scale.x, scale.y) + scalePerRadius * splashRadius;

        /// <summary>Fires every authored burst at the pose, the timed ones later, thinned by density.</summary>
        public void Burst(Vector3 position, Quaternion rotation, float burstScale, float density)
        {
            var emitters = Captured();
            Pose(position, rotation, burstScale);
            for (var e = 0; e < emitters.Length; e++)
            {
                for (var b = 0; b < emitters[e].BurstCount; b++)
                {
                    var count = emitters[e].BurstSize(b, density);
                    var time = emitters[e].BurstTime(b);
                    if (time <= 0f)
                    {
                        emitters[e].Emit(count);
                        continue;
                    }

                    _delayed.Add(new DelayedBurst
                    {
                        Remaining = time, Emitter = e, Count = count,
                        Position = position, Rotation = rotation, Scale = burstScale,
                    });
                }
            }

            Flash();
        }

        /// <summary>
        /// Emits the authored rates for <paramref name="deltaTime"/> and the distance from→to, scaled by strength and
        /// density, spread evenly along the path. <paramref name="carry"/> keeps the fraction of a particle per stream.
        /// </summary>
        public void Stream(Vector3 from, Vector3 to, Quaternion rotation, float deltaTime, float strength,
            float density, ref float carry)
        {
            var emitters = Captured();
            var distance = Vector3.Distance(from, to);
            var total = 0f;
            foreach (var emitter in emitters)
            {
                total += Expected(emitter, deltaTime, distance);
            }

            total *= strength * density;
            var whole = Mathf.FloorToInt(carry + total);
            carry = carry + total - whole;
            if (whole <= 0)
            {
                return;
            }

            // A frame often owes one particle: a weighted draw per particle keeps every emitter at its rate.
            var counts = Counts(emitters.Length);
            for (var n = 0; n < whole; n++)
            {
                counts[Pick(emitters, deltaTime, distance)]++;
            }

            for (var e = 0; e < emitters.Length; e++)
            {
                Spread(emitters[e], counts[e], from, to, rotation);
            }
        }

        private int[] Counts(int length)
        {
            if (_counts == null || _counts.Length != length)
            {
                _counts = new int[length];
            }

            Array.Clear(_counts, 0, length);
            return _counts;
        }

        private static int Pick(EffectEmitter[] emitters, float deltaTime, float distance)
        {
            var total = 0f;
            foreach (var emitter in emitters)
            {
                total += Expected(emitter, deltaTime, distance);
            }

            var pick = UnityEngine.Random.value * total;
            for (var e = 0; e < emitters.Length - 1; e++)
            {
                pick -= Expected(emitters[e], deltaTime, distance);
                if (pick < 0f)
                {
                    return e;
                }
            }

            return emitters.Length - 1;
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
                _emitters[burst.Emitter].Emit(burst.Count);
            }
        }

        private static float Expected(EffectEmitter emitter, float deltaTime, float distance) =>
            emitter.RateOverTime * deltaTime + emitter.RateOverDistance * distance;

        private void Spread(EffectEmitter emitter, int count, Vector3 from, Vector3 to, Quaternion rotation)
        {
            for (var i = 1; i <= count; i++)
            {
                Pose(Vector3.Lerp(from, to, i / (float)count), rotation, 1f);
                emitter.Emit(1);
            }
        }

        private void Pose(Vector3 position, Quaternion rotation, float poseScale)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = Vector3.one * poseScale;
        }

        private void Flash()
        {
            if (Lights == null || EffectDetail.Level != EffectDetailLevel.High)
            {
                return;
            }

            foreach (var light in _lights)
            {
                Lights.Flash(light, light.transform.position);
            }
        }

        /// <summary>Reads the authored emission on first use; every system must simulate in world space.</summary>
        private EffectEmitter[] Captured()
        {
            if (_emitters != null)
            {
                return _emitters;
            }

            if (maxDistance <= 0f)
            {
                throw new InvalidOperationException($"Effect rig '{name}': maxDistance must be above 0.");
            }

            var systems = GetComponentsInChildren<ParticleSystem>(true);
            var subEmitters = SubEmitters(systems);
            var emitters = new List<EffectEmitter>();
            foreach (var system in systems)
            {
                var space = system.main.simulationSpace;
                if (space != ParticleSystemSimulationSpace.World)
                {
                    throw new InvalidOperationException(
                        $"Effect rig '{name}': particle system '{system.name}' simulates in {space} space; " +
                        "rigs need World.");
                }

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
