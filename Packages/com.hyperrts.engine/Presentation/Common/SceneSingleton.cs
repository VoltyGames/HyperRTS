using Unity.Entities;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// A singleton entity a scene object keeps in the presented match world: written into whichever world is presented
    /// now, so a world that appears later or replaces the last one gets it too. Without a match, writes do nothing.
    /// </summary>
    public sealed class SceneSingleton<T> where T : unmanaged, IComponentData
    {
        private World _world;
        private Entity _entity;

        /// <summary>Writes the value, creating the entity in a new or replaced world. Call it every frame.</summary>
        public void Write(in T value)
        {
            // Waits for a match, so nothing lands in a world that is still loading or in the editor's.
            if (!MatchView.TryGetDefault(out var view))
            {
                return;
            }

            var manager = view.EntityManager;
            if (_world != manager.World || !manager.Exists(_entity))
            {
                _world = manager.World;
                _entity = manager.CreateEntity(typeof(T));
            }

            manager.SetComponentData(_entity, value);
        }

        /// <summary>Destroys the entity, so readers see the singleton absent.</summary>
        public void Remove()
        {
            if (_world is { IsCreated: true } && _world.EntityManager.Exists(_entity))
            {
                _world.EntityManager.DestroyEntity(_entity);
            }

            _world = null;
            _entity = Entity.Null;
        }
    }
}
