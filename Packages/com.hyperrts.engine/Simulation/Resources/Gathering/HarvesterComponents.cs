using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Resources.Gathering
{
    /// <summary>Gathering stats and the cargo currently carried.</summary>
    public struct Harvester : IComponentData
    {
        public int Capacity;
        public float GatherRate;

        /// <summary>How far from a depleted node it looks for another of the same type.</summary>
        public float ReplacementRange;

        /// <summary>Seconds spent at the drop-off before the cargo is paid; 0 pays on arrival.</summary>
        public float UnloadSeconds;

        public UnityObjectRef<ResourceType> CargoType;
        [GhostField] public int CargoAmount;

        /// <summary>Takes this frame's share from <paramref name="node"/>, and sends the truck back once full.</summary>
        public void Fill(ref HarvestState state, ref ResourceNode node, float deltaTime)
        {
            if (CargoAmount == 0 || !CargoType.Equals(node.Type))
            {
                CargoType = node.Type;
                CargoAmount = 0;
            }

            state.Progress += GatherRate * deltaTime;
            var whole = (int)state.Progress;
            state.Progress -= whole;
            var taken = math.min(whole, math.min(node.Amount, Capacity - CargoAmount));
            node.Amount -= taken;
            CargoAmount += taken;

            state.Filling = CargoAmount < Capacity;
            if (!state.Filling)
            {
                state.Phase = HarvestPhase.Returning;
                state.Progress = 0f;
                state.DropOff = Entity.Null;
            }
        }

        /// <summary>0 empty to 1 full, draining over the unload while <paramref name="state"/> is unloading.</summary>
        public readonly float LoadFraction(in HarvestState state)
        {
            var load = (float)CargoAmount / Capacity;
            if (state.Phase != HarvestPhase.Unloading)
            {
                return load;
            }

            return load * (1f - math.saturate(state.UnloadTime / UnloadSeconds));
        }
    }

    /// <summary>Leg of the gather loop a harvester is on.</summary>
    public enum HarvestPhase : byte
    {
        Gathering = 0,
        Returning = 1,

        /// <summary>Standing at the drop-off until the unload time passes, when the cargo is paid.</summary>
        Unloading = 2,
    }

    /// <summary>Gather-loop bookkeeping for a <see cref="Harvester"/> on a Gather order.</summary>
    public struct HarvestState : IComponentData
    {
        [GhostField] public HarvestPhase Phase;

        /// <summary>True while it adds cargo at a node this frame.</summary>
        [GhostField] public bool Filling;

        /// <summary>Seconds spent in the current unload.</summary>
        [GhostField(Quantization = 100)] public float UnloadTime;

        /// <summary>Last node gathered from, so a replacement of the same type can be found near it.</summary>
        public float3 NodePosition;

        /// <summary>Fractional gathering carried between frames.</summary>
        public float Progress;

        /// <summary>Where this trip delivers, chosen once so a turning truck doesn't flip between two.</summary>
        public Entity DropOff;

        public void BeginUnload()
        {
            Phase = HarvestPhase.Unloading;
            UnloadTime = 0f;
        }

        /// <summary>A cancelled order or lost drop-off keeps the cargo, so the next trip still delivers it.</summary>
        public void CancelUnload()
        {
            if (Phase == HarvestPhase.Unloading)
            {
                Phase = HarvestPhase.Returning;
            }
        }

    }

    /// <summary>Accepts harvested cargo from its owner's harvesters.</summary>
    public struct ResourceDropOff : IComponentData { }
}
