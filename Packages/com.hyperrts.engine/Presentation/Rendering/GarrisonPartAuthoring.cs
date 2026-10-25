using System.Collections.Generic;
using System.Linq;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// A mesh under a container drawn only while one of the listed passengers is aboard, such as the machine gun a
    /// bunker shows for a rifle squad and hides for an anti-tank team or when empty.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Garrison Part")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class GarrisonPartAuthoring : AuthoringBehaviour
    {
        [Tooltip("Passengers whose presence aboard shows this part; it is hidden with none of them aboard.")]
        public List<UnitAuthoring> shownFor = new();

        public static void Add<TWriter>(ref TWriter writer, Entity container, IEnumerable<int> typeIds)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new GarrisonPart { Container = container });
            var passengers = writer.AddBuffer<GarrisonPartPassenger>();
            foreach (var typeId in typeIds)
            {
                passengers.Add(new GarrisonPartPassenger { TypeId = typeId });
            }
        }

        public class Baker : Baker<GarrisonPartAuthoring>
        {
            public override void Bake(GarrisonPartAuthoring authoring)
            {
                var container = GetComponentInParent<ContainerAuthoring>();
                if (container == null)
                {
                    return;
                }

                foreach (var unit in authoring.shownFor)
                {
                    DependsOn(unit);
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                Add(ref writer, GetEntity(container, TransformUsageFlags.Dynamic), authoring.shownFor.Select(unit => unit.TypeId));
            }
        }
    }
}
