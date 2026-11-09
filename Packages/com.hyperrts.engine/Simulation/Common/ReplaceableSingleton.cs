using System;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A singleton that owns native memory and can be rebuilt in place, see <see cref="SingletonExtensions"/>.</summary>
    public interface IReplaceableSingleton<T> : IDisposable where T : unmanaged
    {
        /// <summary>Takes from the value being replaced what must carry over, before that one is disposed.</summary>
        void Inherit(ref T previous);
    }

    /// <summary>Rebuilds an <see cref="IReplaceableSingleton{T}"/> without leaking the value it replaces.</summary>
    public static class SingletonExtensions
    {
        /// <summary>
        /// Creates the singleton, or waits for its jobs, lets <paramref name="next"/> inherit from the old value,
        /// disposes that and swaps <paramref name="next"/> in; true if it replaced one. <paramref name="query"/> reads
        /// <typeparamref name="T"/> read-write and is built in OnCreate.
        /// </summary>
        public static bool ReplaceSingleton<T>(this ref SystemState state, EntityQuery query, ref T next,
            in FixedString64Bytes name)
            where T : unmanaged, IComponentData, IReplaceableSingleton<T>
        {
            if (query.IsEmptyIgnoreFilter)
            {
                state.EntityManager.CreateSingleton(next, name);
                return false;
            }

            state.CompleteDependency();
            ref var current = ref query.GetSingletonRW<T>().ValueRW;
            next.Inherit(ref current);
            current.Dispose();
            current = next;
            return true;
        }
    }
}
