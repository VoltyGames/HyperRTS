using System;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Splits a stream step's particles among a rig's emitters at their authored rates.</summary>
    public sealed class StreamShares
    {
        private int[] _counts;
        private float[] _weights;

        public int[] Split(EffectEmitter[] emitters, float deltaTime, float distance, float scale, ref float carry)
        {
            var expected = Weigh(emitters, deltaTime, distance);
            var total = expected * scale;
            var whole = Mathf.FloorToInt(carry + total);
            carry = carry + total - whole;
            var counts = Counts(emitters.Length);

            // A frame often owes one particle: a weighted draw per particle keeps every emitter at its rate.
            for (var n = 0; n < whole; n++)
            {
                counts[Pick(expected)]++;
            }

            return counts;
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

        private float Weigh(EffectEmitter[] emitters, float deltaTime, float distance)
        {
            if (_weights == null || _weights.Length != emitters.Length)
            {
                _weights = new float[emitters.Length];
            }

            var total = 0f;
            for (var e = 0; e < emitters.Length; e++)
            {
                _weights[e] = emitters[e].RateOverTime * deltaTime + emitters[e].RateOverDistance * distance;
                total += _weights[e];
            }

            return total;
        }

        private int Pick(float total)
        {
            var pick = UnityEngine.Random.value * total;
            for (var e = 0; e < _weights.Length - 1; e++)
            {
                pick -= _weights[e];
                if (pick < 0f)
                {
                    return e;
                }
            }

            return _weights.Length - 1;
        }
    }
}
