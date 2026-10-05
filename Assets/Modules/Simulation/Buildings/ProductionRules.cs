using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Option-list rules shared by the build and production commands.</summary>
    public static class ProductionRules
    {
        public static bool Offers<T>(DynamicBuffer<T> options, Entity prefab)
            where T : unmanaged, IBufferElementData, IPrefabOption
        {
            foreach (var option in options)
            {
                if (option.Prefab == prefab)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
