using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.References
{
    /// <summary>
    /// Server: stamps <see cref="ResourceStock.TypeId"/> on new stockpile entries. Stock arithmetic runs in Burst jobs,
    /// which can't read the asset behind <see cref="ResourceStock.Type"/>.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(GhostSendSystem))]
    public partial struct ResourceIdSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var stock in SystemAPI.Query<DynamicBuffer<ResourceStock>>())
            {
                for (var i = 0; i < stock.Length; i++)
                {
                    if (stock[i].TypeId == 0 && stock[i].Type.IsValid())
                    {
                        stock.ElementAt(i).TypeId = stock[i].Type.Value.Id;
                    }
                }
            }
        }
    }
}
