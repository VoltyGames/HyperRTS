using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Shadowless, flickering point lights given each frame to the streamed loops nearest the camera whose rigs carry a
    /// <see cref="LoopLight"/>, such as burning wrecks and buildings. High detail only, beside <see cref="EffectLights"/>.
    /// </summary>
    public sealed class LoopLights
    {
        public const int Capacity = 8;

        private readonly Light[] _lights = new Light[Capacity];
        private readonly Transform _root;

        public LoopLights(Transform root) => _root = root;

        /// <summary>The loops offered this frame; cleared by <see cref="Show"/>.</summary>
        public LoopOffers<LoopLight> Offers { get; } = new();

        /// <summary>Lights the nearest offers up to the cap on High, puts out the rest and clears the offers.</summary>
        public void Show(Vector3 eye, DetailLevel level, float time)
        {
            var lit = 0;
            if (level == DetailLevel.High)
            {
                Offers.SortFrom(eye);
                lit = Mathf.Min(Capacity, Offers.Count);
            }

            for (var i = 0; i < lit; i++)
            {
                Light(i, Offers[i], time);
            }

            for (var i = lit; i < Capacity; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].enabled = false;
                }
            }

            Offers.Clear();
        }

        private void Light(int slot, LoopOffers<LoopLight>.Offer offer, float time)
        {
            var source = offer.Source;
            var light = Slot(slot);
            light.transform.position = offer.Position + Vector3.up * source.height;
            light.color = source.color;
            light.range = source.range;
            // The stream key spreads phases, so neighbouring fires don't pulse together.
            light.intensity = source.IntensityAt(time, offer.Key % 6283u * 0.001f, offer.Strength);
            light.enabled = true;
        }

        private Light Slot(int slot)
        {
            if (_lights[slot] != null)
            {
                return _lights[slot];
            }

            var host = new GameObject($"Loop Light {slot}") { hideFlags = HideFlags.DontSave };
            host.transform.SetParent(_root);
            var light = host.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            return _lights[slot] = light;
        }
    }
}
