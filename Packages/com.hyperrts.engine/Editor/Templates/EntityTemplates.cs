using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Upgrades;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// Ready-to-bake GameObjects per role: a root with collider, authoring and ghost (so it replicates), and a primitive
    /// Model child in the team-coloured material.
    /// </summary>
    public static class EntityTemplates
    {
        private const string TeamMaterial = "Packages/com.hyperrts.engine/Presentation/Materials/TeamLit.mat";

        public static GameObject Unit(string name = "Unit")
        {
            var go = Template(name, PrimitiveType.Capsule, new Vector3(0.8f, 0.9f, 0.8f), 0.9f);
            UnitRenderingLayer.Assign(go, UnitRenderingLayer.Index);
            go.AddComponent<GhostAuthoringComponent>();
            var unit = go.AddComponent<UnitAuthoring>();
            unit.displayName = name;
            unit.radius = 0.4f;
            return go;
        }

        public static GameObject CombatUnit() => Unit("Combat Unit").AddComponent<WeaponAuthoring>().gameObject;
        public static GameObject Worker() => Unit("Worker").AddComponent<BuilderAuthoring>().gameObject;
        public static GameObject Harvester() => Unit("Harvester").AddComponent<HarvesterAuthoring>().gameObject;
        public static GameObject Transport() => Unit("Transport").AddComponent<ContainerAuthoring>().gameObject;
        public static GameObject Aircraft() => Unit("Aircraft").AddComponent<FlightAuthoring>().gameObject;

        public static GameObject Building(string name = "Building", float width = 4f, float height = 2.5f)
        {
            var go = Template(name, PrimitiveType.Cube, new Vector3(width, height, width), height * 0.5f);
            go.AddComponent<GhostAuthoringComponent>();
            var building = go.AddComponent<BuildingAuthoring>();
            building.displayName = name;
            building.footprint = new Vector2(width, width);
            return go;
        }

        public static GameObject Producer() => Building("Producer").AddComponent<ProducerAuthoring>().gameObject;
        public static GameObject DropOff() => Building("Resource Drop-Off").AddComponent<ResourceDropOffAuthoring>().gameObject;

        /// <summary>A producer meant for research: list Upgrade prefabs in its research options.</summary>
        public static GameObject ResearchBuilding() =>
            Building("Research Building").AddComponent<ProducerAuthoring>().gameObject;

        public static GameObject Airfield() =>
            Building("Airfield", 8f, 0.5f).AddComponent<AirfieldAuthoring>().gameObject;

        /// <summary>A neutral building that units with a Capturer can take over.</summary>
        public static GameObject CapturableBuilding()
        {
            var go = Building("Capturable Building");
            go.GetComponent<BuildingAuthoring>().owner = 0;
            go.AddComponent<CapturableAuthoring>();
            return go;
        }

        /// <summary>A research prefab (no model) for a producer's research options.</summary>
        public static GameObject Upgrade()
        {
            var go = new GameObject("Upgrade");
            go.AddComponent<UpgradeAuthoring>().displayName = go.name;
            return go;
        }

        public static GameObject DefenseTower()
        {
            var go = Building("Defense Tower", 2f, 4f);
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
            var go = Template("Nav Obstacle", PrimitiveType.Cube, new Vector3(4f, 2f, 4f), 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<NavObstacleAuthoring>().size = new Vector2(4f, 4f);
            return go;
        }

        public static GameObject NavArea()
        {
            var go = new GameObject("Nav Area");
            go.AddComponent<NavAreaAuthoring>();
            return go;
        }

        // Root keeps uniform scale for clean transforms; the visible model is a scaled child.
        private static GameObject Template(string name, PrimitiveType shape, Vector3 size, float centerY)
        {
            var root = new GameObject(name);
            var model = GameObject.CreatePrimitive(shape);
            model.name = "Model";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TeamMaterial)
                ?? throw new System.IO.FileNotFoundException($"HyperRTS: missing template material {TeamMaterial}.");
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
