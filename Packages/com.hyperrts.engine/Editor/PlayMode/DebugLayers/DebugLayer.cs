using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.DebugLayers
{
    /// <summary>One view of live simulation state in the Game and Scene views, switched in the Cheats &amp; Debug window. Subclasses are discovered.</summary>
    public abstract class DebugLayer
    {
        /// <summary>The short row name in the window.</summary>
        public abstract string Label { get; }

        /// <summary>What the layer draws and what its colours mean, shown as the row's tooltip.</summary>
        public abstract string Detail { get; }

        /// <summary>The heading the row sits under, such as "Movement".</summary>
        public abstract string Group { get; }

        /// <summary>The row's glyph: a name from Icons/Glyphs.uss.</summary>
        public abstract string Glyph { get; }

        /// <summary>Remembered per layer type in the editor state, so switches survive domain reloads and restarts.</summary>
        public bool Shown
        {
            get => EditorState.instance.IsLayerShown(GetType().FullName);
            set => EditorState.instance.ShowLayer(GetType().FullName, value);
        }

        /// <summary>The world drawn: server-side truth by default; <see cref="SimulationWorlds.Presented"/> for client-only state.</summary>
        public virtual WorldSystemFilterFlags Source => SimulationWorlds.Authoritative;

        /// <summary>Adds this refresh's lines and cells to <paramref name="shapes"/>, from the <see cref="Source"/> world with its jobs completed.</summary>
        public abstract void Draw(EntityManager entityManager, DebugShapes shapes);

        /// <summary>A short status shown under the row while the layer is on, such as totals; null for none.</summary>
        public virtual string Describe(EntityManager entityManager) => null;

        // PlayWorld is internal, so layers in a game's assembly reach it through here.
        protected static bool TryGetSingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData =>
            PlayWorld.TryGetSingleton(entityManager, out value);
    }
}
