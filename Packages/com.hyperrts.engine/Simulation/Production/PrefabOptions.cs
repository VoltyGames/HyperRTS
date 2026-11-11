using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Looks a prefab up in any option buffer (units to train, buildings to place), whatever its element type.</summary>
    public static class PrefabOptions
    {
        public static bool Has<T>(DynamicBuffer<T> options, Entity prefab)
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
