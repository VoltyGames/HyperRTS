using HyperRTS.Core;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>One Scene-view visualisation of live simulation state, toggled from the debug overlay. Subclasses are discovered.</summary>
    public abstract class DebugLayer
    {
        public abstract string Label { get; }

        public bool Enabled { get; set; }

        /// <summary>The world drawn: server-side truth by default; <see cref="SimulationWorlds.Presented"/> for client-only state.</summary>
        public virtual WorldSystemFilterFlags Source => SimulationWorlds.Authoritative;

        /// <summary>Called on Repaint in Play mode with the <see cref="Source"/> world, its jobs completed.</summary>
        public abstract void Draw(EntityManager entityManager);

        // PlayWorld is internal, so layers in a game's assembly reach it through here.
        protected static bool TryGetSingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData =>
            PlayWorld.TryGetSingleton(entityManager, out value);
    }
}
