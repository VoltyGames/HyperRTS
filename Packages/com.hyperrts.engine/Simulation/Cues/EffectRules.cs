using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Rig lookup in an entity's effects, shared by the simulation and presentation.</summary>
    public static class EffectRules
    {
        public static bool TryGetSocket(DynamicBuffer<EntityEffect> effects, CueSlot slot, out float3 socket)
        {
            foreach (var effect in effects)
            {
                if (effect.Slot == slot)
                {
                    socket = effect.Socket;
                    return true;
                }
            }

            socket = float3.zero;
            return false;
        }
    }
}
