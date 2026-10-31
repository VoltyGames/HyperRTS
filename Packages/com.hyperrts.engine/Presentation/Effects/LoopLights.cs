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
        public const int Capacity = LightSlots.Capacity;

        private readonly LightSlots _slots;

        public LoopLights(Transform root) => _slots = new LightSlots(root, "Loop Light");

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

            _slots.DisableFrom(lit);
            Offers.Clear();
        }

        private void Light(int slot, LoopOffers<LoopLight>.Offer offer, float time)
        {
            var source = offer.Source;
            var light = _slots.Get(slot);
            light.transform.position = offer.Position + Vector3.up * source.height;
            light.color = source.color;
            light.range = source.range;
            // The stream key spreads phases, so neighbouring fires don't pulse together.
            light.intensity = source.IntensityAt(time, offer.Key % 6283u * 0.001f, offer.Strength);
            light.enabled = true;
        }
    }
}
