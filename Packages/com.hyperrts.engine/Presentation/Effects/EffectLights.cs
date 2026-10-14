using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Shadowless point lights shared by every rig's flashes; when all are lit the oldest is reused.</summary>
    public sealed class EffectLights
    {
        public const int Capacity = 8;

        private readonly Light[] _lights = new Light[Capacity];
        private readonly EffectLight[] _sources = new EffectLight[Capacity];
        private readonly float[] _ages = new float[Capacity];
        private readonly Transform _root;

        public EffectLights(Transform root) => _root = root;

        public void Flash(EffectLight source, Vector3 position)
        {
            var slot = Oldest();
            var light = Light(slot);
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
            for (var i = 0; i < Capacity; i++)
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
                    _lights[i].enabled = false;
                    continue;
                }

                _lights[i].intensity = source.intensity * source.falloff.Evaluate(life);
            }
        }

        private int Oldest()
        {
            var oldest = 0;
            for (var i = 0; i < Capacity; i++)
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

        private Light Light(int slot)
        {
            if (_lights[slot] != null)
            {
                return _lights[slot];
            }

            var host = new GameObject($"Flash {slot}") { hideFlags = HideFlags.DontSave };
            host.transform.SetParent(_root);
            var light = host.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            return _lights[slot] = light;
        }
    }
}
