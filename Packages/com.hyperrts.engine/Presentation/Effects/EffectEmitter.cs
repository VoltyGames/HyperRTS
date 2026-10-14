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
            system.Clear(false);
            system.Play(false);
        }

        /// <summary>Particles a second, from the authored rate over time.</summary>
        public float RateOverTime { get; }

        /// <summary>Particles a metre the rig moves, from the authored rate over distance.</summary>
        public float RateOverDistance { get; }

        public int BurstCount => _bursts.Length;

        /// <summary>Seconds after the burst starts that this authored burst fires.</summary>
        public float BurstTime(int index) => _bursts[index].time;

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

        public void Emit(int count)
        {
            if (count > 0)
            {
                _system.Emit(count);
            }
        }
    }
}
