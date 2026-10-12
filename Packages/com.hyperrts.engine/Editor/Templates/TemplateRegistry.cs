using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// Every role template, listed once. GameObject ▸ HyperRTS and Assets ▸ Create ▸ HyperRTS ▸ Prefabs both use these
    /// paths, so the two menus offer the same set under the same names.
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

        public static readonly IReadOnlyList<EntityTemplate> All = new[]
        {
            new EntityTemplate(Unit, () => EntityTemplates.Unit()),
            new EntityTemplate(CombatUnit, EntityTemplates.CombatUnit),
            new EntityTemplate(Worker, EntityTemplates.Worker),
            new EntityTemplate(Harvester, EntityTemplates.Harvester),
            new EntityTemplate(Transport, EntityTemplates.Transport),
            new EntityTemplate(Aircraft, EntityTemplates.Aircraft),
            new EntityTemplate(Building, () => EntityTemplates.Building()),
            new EntityTemplate(Producer, EntityTemplates.Producer),
            new EntityTemplate(ResearchBuilding, EntityTemplates.ResearchBuilding),
            new EntityTemplate(DropOff, EntityTemplates.DropOff),
            new EntityTemplate(DefenseTower, EntityTemplates.DefenseTower),
            new EntityTemplate(Airfield, EntityTemplates.Airfield),
            new EntityTemplate(CapturableBuilding, EntityTemplates.CapturableBuilding),
            new EntityTemplate(Upgrade, EntityTemplates.Upgrade),
            new EntityTemplate(ResourceNode, EntityTemplates.ResourceNode),
            new EntityTemplate(NavObstacle, EntityTemplates.NavObstacle),
            new EntityTemplate(NavArea, EntityTemplates.NavArea),
        };

        public static EntityTemplate Get(string path) =>
            All.FirstOrDefault(template => template.Path == path)
            ?? throw new ArgumentException($"No HyperRTS template has the path '{path}'.");
    }
}
