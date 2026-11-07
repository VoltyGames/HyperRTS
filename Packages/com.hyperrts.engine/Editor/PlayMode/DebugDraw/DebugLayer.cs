using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>One Scene-view visualisation of live simulation state, toggled from the debug overlay. Subclasses are discovered.</summary>
    public abstract class DebugLayer
    {
        /// <summary>The short row name in the overlay.</summary>
        public abstract string Label { get; }

        /// <summary>What the layer draws, shown as the row's tooltip.</summary>
        public abstract string Detail { get; }

        /// <summary>The overlay heading the row sits under, such as "Movement".</summary>
        public abstract string Group { get; }

        /// <summary>The row's glyph: a name from Icons/Glyphs.uss.</summary>
        public abstract string Glyph { get; }

        /// <summary>Remembered per layer type in the editor state, so toggles survive domain reloads and restarts.</summary>
        public bool Shown
        {
            get => EditorState.instance.IsLayerShown(GetType().FullName);
            set => EditorState.instance.ShowLayer(GetType().FullName, value);
        }

        /// <summary>The world drawn: server-side truth by default; <see cref="SimulationWorlds.Presented"/> for client-only state.</summary>
        public virtual WorldSystemFilterFlags Source => SimulationWorlds.Authoritative;

        /// <summary>Called on Repaint in Play mode with the <see cref="Source"/> world, its jobs completed.</summary>
        public abstract void Draw(EntityManager entityManager);

        // PlayWorld is internal, so layers in a game's assembly reach it through here.
        protected static bool TryGetSingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData =>
            PlayWorld.TryGetSingleton(entityManager, out value);
    }
}
