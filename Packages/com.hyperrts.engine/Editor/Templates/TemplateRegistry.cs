using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// Every role template, placed in a scene from the GameObject menu or saved as a prefab by the New Entity wizard.
    /// The path constants exist for the menu attributes; <see cref="All"/> is the list.
    /// </summary>
    public static class TemplateRegistry
    {
        public const string Unit = "Units/Unit";
        public const string CombatUnit = "Units/Combat Unit";
        public const string Worker = "Units/Worker";
        public const string Harvester = "Units/Harvester";
        public const string Transport = "Units/Transport";
        public const string Aircraft = "Units/Aircraft";
        public const string Building = "Buildings/Building";
        public const string Producer = "Buildings/Producer";
        public const string ResearchBuilding = "Buildings/Research Building";
        public const string DropOff = "Buildings/Resource Drop-Off";
        public const string DefenseTower = "Buildings/Defense Tower";
        public const string Airfield = "Buildings/Airfield";
        public const string CapturableBuilding = "Buildings/Capturable Building";
        public const string Upgrade = "Research/Upgrade";
        public const string ResourceNode = "Map/Resource Node";
        public const string NavObstacle = "Map/Nav Obstacle";
        public const string NavArea = "Map/Nav Area";

        [NoAutoStaticsCleanup]
        public static readonly IReadOnlyList<EntityTemplate> All = new[]
        {
            new EntityTemplate(Unit, () => TemplateObjects.Unit()),
            new EntityTemplate(CombatUnit, TemplateObjects.CombatUnit),
            new EntityTemplate(Worker, TemplateObjects.Worker),
            new EntityTemplate(Harvester, TemplateObjects.Harvester),
            new EntityTemplate(Transport, TemplateObjects.Transport),
            new EntityTemplate(Aircraft, TemplateObjects.Aircraft),
            new EntityTemplate(Building, () => TemplateObjects.Building()),
            new EntityTemplate(Producer, TemplateObjects.Producer),
            new EntityTemplate(ResearchBuilding, TemplateObjects.ResearchBuilding),
            new EntityTemplate(DropOff, TemplateObjects.DropOff),
            new EntityTemplate(DefenseTower, TemplateObjects.DefenseTower),
            new EntityTemplate(Airfield, TemplateObjects.Airfield),
            new EntityTemplate(CapturableBuilding, TemplateObjects.CapturableBuilding),
            new EntityTemplate(Upgrade, TemplateObjects.Upgrade),
            new EntityTemplate(ResourceNode, TemplateObjects.ResourceNode),
            new EntityTemplate(NavObstacle, TemplateObjects.NavObstacle),
            new EntityTemplate(NavArea, TemplateObjects.NavArea),
        };

        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, EntityTemplate> ByPath = All.ToDictionary(template => template.Path);

        public static EntityTemplate Get(string path) =>
            ByPath.TryGetValue(path, out var template)
                ? template
                : throw new ArgumentException($"No HyperRTS template has the path '{path}'.");
    }
}
