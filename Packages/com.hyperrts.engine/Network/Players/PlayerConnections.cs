using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Server: connection <see cref="Unity.NetCode.NetworkId"/> to the player slot it controls.</summary>
    public static class PlayerConnections
    {
        /// <summary><paramref name="players"/> must match <see cref="PlayerConnection"/>; skips free slots.</summary>
        public static NativeHashMap<int, Entity> ByNetworkId(EntityQuery players)
        {
            var entities = players.ToEntityArray(Allocator.Temp);
            var links = players.ToComponentDataArray<PlayerConnection>(Allocator.Temp);
            var result = new NativeHashMap<int, Entity>(entities.Length, Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (links[i].NetworkId != 0)
                {
                    result[links[i].NetworkId] = entities[i];
                }
            }

            return result;
        }
    }
}
