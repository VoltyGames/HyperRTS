using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Air
{
    /// <summary>The landing pads of a producer that builds aircraft: each new one needs a free pad and starts on it.</summary>
    public struct AirfieldPads
    {
        [ReadOnly] private BufferLookup<LandingPad> _pads;
        [ReadOnly] private ComponentLookup<HomePad> _homes;

        public AirfieldPads(ref SystemState state)
        {
            _pads = state.GetBufferLookup<LandingPad>(true);
            _homes = state.GetComponentLookup<HomePad>(true);
        }

        public void Update(ref SystemState state)
        {
            _pads.Update(ref state);
            _homes.Update(ref state);
        }

        /// <summary>
        /// A free pad of the aircraft's kind at <paramref name="producer"/>, or -1 for a unit that needs none. False
        /// when every such pad is taken; <see cref="PadSystem"/> claims the pad once the unit exists.
        /// </summary>
        public readonly bool TryReserve(Entity producer, Entity prefab, out int pad)
        {
            pad = -1;
            if (!_homes.HasComponent(prefab) || !_pads.TryGetBuffer(producer, out var pads))
            {
                return true;
            }

            pad = LandingPad.FindFree(pads, _homes[prefab].Kind);
            return pad >= 0;
        }

        /// <summary>The pad's spot, or <paramref name="offset"/> from the producer when there is no pad.</summary>
        public readonly float3 SpawnPoint(Entity producer, int pad, float3 offset, in LocalTransform transform) =>
            transform.TransformPoint(pad >= 0 ? _pads[producer][pad].Offset : offset);

        /// <summary>Homes the new aircraft on its pad, landed.</summary>
        public readonly void Dock(EntityCommandBuffer ecb, Entity unit, Entity prefab, Entity producer, int pad)
        {
            if (pad < 0)
            {
                return;
            }

            ecb.SetComponent(unit, _homes[prefab].ToPad(producer, pad));
            ecb.SetComponentEnabled<Docked>(unit, true);
        }
    }
}
