using Unity.Entities;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>One Scene-view visualisation of live simulation state, toggled from the debug overlay. Subclasses are discovered.</summary>
    public abstract class DebugLayer
    {
        public abstract string Label { get; }

        public bool Enabled { get; set; }

        /// <summary>Called on Repaint in Play mode, with the world's jobs completed.</summary>
        public abstract void Draw(EntityManager entityManager);

        // PlayWorld is internal, so layers in a game's assembly reach it through here.
        protected static bool TryGetSingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData =>
            PlayWorld.TryGetSingleton(entityManager, out value);
    }
}
