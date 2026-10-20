using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// One particle system of a rig, emitting only on request: its authored bursts and rates are read once, then its
    /// own emission is switched off while it keeps playing, so particles still simulate and sub-emitters still fire.
    /// </summary>
    public sealed class EffectEmitter
    {
        private readonly ParticleSystem _system;
        private readonly ParticleSystem.Burst[] _bursts;

        public EffectEmitter(ParticleSystem system)
        {
            _system = system;
            var emission = system.emission;
            _bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(_bursts);
            RateOverTime = emission.enabled ? emission.rateOverTimeMultiplier : 0f;
            RateOverDistance = emission.enabled ? emission.rateOverDistanceMultiplier : 0f;
            emission.enabled = false;
            var main = system.main;
            main.loop = true;
            // One pooled system serves every instance on the map, so the authored cap would starve busy battles and
            // culling would pause it as the pose jumps; cost follows live particles, not the cap.
            main.maxParticles = Mathf.Max(main.maxParticles, MaxParticles);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            system.Clear(false);
            system.Play(false);
        }

        /// <summary>Particles a second, from the authored rate over time.</summary>
        public float RateOverTime { get; }

        /// <summary>Particles a metre the rig moves, from the authored rate over distance.</summary>
        public float RateOverDistance { get; }

        public int BurstCount => _bursts.Length;

        public int BurstCycles(int index) => _bursts[index].cycleCount;

        public float BurstTime(int index, int cycle) => _bursts[index].time + cycle * _bursts[index].repeatInterval;

        /// <summary>
        /// The burst's particle count thinned by density, at least one while it has any; 0 when its probability fails.
        /// </summary>
        public int BurstSize(int index, float density)
        {
            var burst = _bursts[index];
            if (Random.value > burst.probability)
            {
                return 0;
            }

            var count = burst.count.Evaluate(0f, Random.value);
            return count > 0f ? Mathf.Max(1, Mathf.RoundToInt(count * density)) : 0;
        }

        /// <summary>The particle cap every pooled system gets at least.</summary>
        public const int MaxParticles = 20000;

        /// <summary>Emits at the system's pose, each particle's start size grown by scale.</summary>
        public void Emit(int count, float scale = 1f)
        {
            if (Mathf.Approximately(scale, 1f))
            {
                _system.Emit(Mathf.Max(count, 0));
                return;
            }

            for (var i = 0; i < count; i++)
            {
                _system.Emit(Scaled(scale), 1);
            }
        }

        private ParticleSystem.EmitParams Scaled(float scale)
        {
            var main = _system.main;
            var lerp = Random.value;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            if (main.startSize3D)
            {
                emit.startSize3D = scale * new Vector3(main.startSizeX.Evaluate(0f, lerp),
                    main.startSizeY.Evaluate(0f, lerp), main.startSizeZ.Evaluate(0f, lerp));
            }
            else
            {
                emit.startSize = scale * main.startSize.Evaluate(0f, lerp);
            }

            return emit;
        }
    }
}
