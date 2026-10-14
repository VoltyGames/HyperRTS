using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>Rig lookup and who sees an effect, shared by the simulation, the network and presentation.</summary>
    public static class EffectRules
    {
        public static bool TryGetSocket(DynamicBuffer<EntityEffect> effects, EffectSlot slot, out float3 socket)
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

        /// <summary>
        /// Whoever could see its source there sees the effect, by the fog and stealth rule of entities. Without a fog
        /// grid everyone does.
        /// </summary>
        public static bool IsVisible(in EffectEvent effect, byte viewer, in FogOfWar fog, in FactionRelations relations)
        {
            if (!fog.IsCreated)
            {
                return true;
            }

            return !fog.IsHiddenFrom(relations, viewer, effect.Faction, effect.Position, effect.Stealthed);
        }

        /// <summary>Whether the local player sees the effect: all of them without a local fog view.</summary>
        public static bool IsSeenLocally(in EffectEvent effect, in LocalFogView view, in FogOfWar fog,
            in FactionRelations relations)
        {
            if (!view.Active)
            {
                return true;
            }

            return IsVisible(effect, view.Viewer, fog, relations);
        }
    }
}
