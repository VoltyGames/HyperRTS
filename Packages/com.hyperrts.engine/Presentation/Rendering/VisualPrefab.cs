using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Copies a gameplay prefab down to what draws it (transforms, meshes, material properties), so its instances are
    /// scenery no simulation, network, physics or selection code ever sees.
    /// </summary>
    public static class VisualPrefab
    {
        public static Entity Build(EntityManager entityManager, Entity prefab)
        {
            var root = entityManager.Instantiate(prefab);
            using var group = Members(entityManager, root);
            foreach (var entity in group)
            {
                Strip(entityManager, entity);
                entityManager.AddComponent<Prefab>(entity);
            }

            return root;
        }

        /// <summary>The root and every entity in its linked group.</summary>
        public static NativeList<Entity> Members(EntityManager entityManager, Entity root)
        {
            var members = new NativeList<Entity>(Allocator.Temp);
            if (!entityManager.HasBuffer<LinkedEntityGroup>(root))
            {
                members.Add(root);
                return members;
            }

            members.AddRange(entityManager.GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray());
            return members;
        }

        private static void Strip(EntityManager entityManager, Entity entity)
        {
            using var types = entityManager.GetComponentTypes(entity, Allocator.Temp);
            foreach (var type in types)
            {
                if (!IsVisual(type))
                {
                    entityManager.RemoveComponent(entity, type);
                }
            }
        }

        private static bool IsVisual(ComponentType type)
        {
            var managed = TypeManager.GetType(type.TypeIndex);
            // Scene membership is kept so copies unload with the scene that loaded their source.
            if (managed == typeof(LinkedEntityGroup) || managed == typeof(SceneSection) || managed == typeof(SceneTag))
            {
                return true;
            }

            // Matched by the packages' assemblies through their types, so a namespace rename can't strip a renderer.
            var rendering = managed.Assembly == typeof(MaterialMeshInfo).Assembly;
            var transform = managed.Assembly == typeof(LocalTransform).Assembly;
            if (rendering || transform)
            {
                return true;
            }

            return Attribute.IsDefined(managed, typeof(MaterialPropertyAttribute));
        }
    }
}
