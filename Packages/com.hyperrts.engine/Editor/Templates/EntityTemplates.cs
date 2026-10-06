using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// Ready-to-bake GameObjects per role: a root with collider, authoring and ghost (so it replicates), and a primitive
    /// Model child.
    /// </summary>
    public static class EntityTemplates
    {
        public static GameObject Unit(string name = "Unit")
        {
            var go = Template(name, PrimitiveType.Capsule, new Vector3(0.8f, 0.9f, 0.8f), 0.9f);
            go.AddComponent<GhostAuthoringComponent>();
            var unit = go.AddComponent<UnitAuthoring>();
            unit.displayName = name;
            unit.radius = 0.4f;
            return go;
        }

        public static GameObject CombatUnit() => Unit("Soldier").AddComponent<WeaponAuthoring>().gameObject;
        public static GameObject Worker() => Unit("Worker").AddComponent<BuilderAuthoring>().gameObject;
        public static GameObject Harvester() => Unit("Harvester").AddComponent<HarvesterAuthoring>().gameObject;

        public static GameObject Building(string name = "Building", float width = 4f, float height = 2.5f)
        {
            var go = Template(name, PrimitiveType.Cube, new Vector3(width, height, width), height * 0.5f);
            go.AddComponent<GhostAuthoringComponent>();
            var building = go.AddComponent<BuildingAuthoring>();
            building.displayName = name;
            building.footprint = new Vector2(width, width);
            return go;
        }

        public static GameObject Producer() => Building("Barracks").AddComponent<ProducerAuthoring>().gameObject;
        public static GameObject DropOff() => Building("Depot").AddComponent<ResourceDropOffAuthoring>().gameObject;

        public static GameObject DefenseTower()
        {
            var go = Building("Tower", 2f, 4f);
            go.AddComponent<WeaponAuthoring>().range = 8f;
            return go;
        }

        public static GameObject ResourceNode()
        {
            var go = Template("Resource Node", PrimitiveType.Cylinder, new Vector3(2.4f, 0.6f, 2.4f), 0.6f);
            go.AddComponent<GhostAuthoringComponent>();
            go.AddComponent<ResourceNodeAuthoring>();
            go.AddComponent<NavObstacleAuthoring>().size = new Vector2(2f, 2f);
            return go;
        }

        public static GameObject NavObstacle()
        {
            var go = Template("Obstacle", PrimitiveType.Cube, new Vector3(4f, 2f, 4f), 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<NavObstacleAuthoring>().size = new Vector2(4f, 4f);
            return go;
        }

        // Root keeps uniform scale for clean transforms; the visible model is a scaled child.
        private static GameObject Template(string name, PrimitiveType shape, Vector3 size, float centerY)
        {
            var root = new GameObject(name);
            var model = GameObject.CreatePrimitive(shape);
            model.name = "Model";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, centerY, 0f);
            model.transform.localScale = size;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = model.transform.localPosition;
            collider.size = shape == PrimitiveType.Capsule ? new Vector3(size.x, size.y * 2f, size.z) : size;
            return root;
        }
    }
}
