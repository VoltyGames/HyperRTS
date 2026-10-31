using UnityEngine;

namespace HyperRTS.Presentation.Effects.Lights
{
    /// <summary>Shadowless point lights shared by every rig's flashes; when all are lit the oldest is reused.</summary>
    public sealed class EffectLights
    {
        private readonly LightSlots _slots;
        private readonly EffectLight[] _sources = new EffectLight[LightSlots.Capacity];
        private readonly float[] _ages = new float[LightSlots.Capacity];

        public EffectLights(Transform root) => _slots = new LightSlots(root, "Flash");

        public void Flash(EffectLight source, Vector3 position)
        {
            var slot = Oldest();
            var light = _slots.Get(slot);
            _sources[slot] = source;
            _ages[slot] = 0f;
            light.transform.position = position;
            light.color = source.color;
            light.range = source.range;
            light.intensity = source.intensity * source.falloff.Evaluate(0f);
            light.enabled = true;
        }

        public void Tick(float deltaTime)
        {
            for (var i = 0; i < LightSlots.Capacity; i++)
            {
                if (_sources[i] == null)
                {
                    continue;
                }

                _ages[i] += deltaTime;
                var source = _sources[i];
                var life = source.duration > 0f ? _ages[i] / source.duration : 1f;
                if (life >= 1f)
                {
                    _sources[i] = null;
                    _slots.Get(i).enabled = false;
                    continue;
                }

                _slots.Get(i).intensity = source.intensity * source.falloff.Evaluate(life);
            }
        }

        private int Oldest()
        {
            var oldest = 0;
            for (var i = 0; i < LightSlots.Capacity; i++)
            {
                if (_sources[i] == null)
                {
                    return i;
                }

                if (_ages[i] > _ages[oldest])
                {
                    oldest = i;
                }
            }

            return oldest;
        }
    }
}
