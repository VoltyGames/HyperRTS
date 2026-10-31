using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>A fixed set of shadowless point lights, each made on first use under a root.</summary>
    public sealed class LightSlots
    {
        public const int Capacity = 8;

        private readonly Light[] _lights = new Light[Capacity];
        private readonly Transform _root;
        private readonly string _name;

        public LightSlots(Transform root, string name)
        {
            _root = root;
            _name = name;
        }

        /// <summary>The slot's light, made when first asked for.</summary>
        public Light Get(int slot)
        {
            if (_lights[slot] != null)
            {
                return _lights[slot];
            }

            var host = new GameObject($"{_name} {slot}") { hideFlags = HideFlags.DontSave };
            host.transform.SetParent(_root);
            var light = host.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            return _lights[slot] = light;
        }

        /// <summary>Puts out the made lights from the slot on.</summary>
        public void DisableFrom(int first)
        {
            for (var i = first; i < Capacity; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].enabled = false;
                }
            }
        }
    }
}
