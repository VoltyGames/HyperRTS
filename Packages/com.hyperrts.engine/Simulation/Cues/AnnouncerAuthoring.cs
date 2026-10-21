using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// The match's announcers: which <see cref="AnnouncerSet"/> each side's players hear for alerts such as unit lost,
    /// base under attack and low power. Place one in the match's subscene.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Audio + "Announcer")]
    [Icon(HyperRTSIcons.Audio)]
    [HelpURL(HyperRTSDocs.Audio)]
    [DisallowMultipleComponent]
    public class AnnouncerAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The game's army id (PlayerSide); 0 for players without a side.")]
            public byte side;

            [Tooltip("Lines this side's players hear.")]
            public AnnouncerSet set;
        }

        [Tooltip("One announcer per side; a side without one hears no alerts.")]
        public List<Entry> sides = new();

        public static DynamicBuffer<AnnouncerVoice> Add<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<AnnouncerVoice>();

        public class Baker : Baker<AnnouncerAuthoring>
        {
            public override void Bake(AnnouncerAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.None));
                var voices = AnnouncerAuthoring.Add(ref writer);
                foreach (var entry in authoring.sides)
                {
                    if (entry.set == null)
                    {
                        throw new InvalidOperationException($"{authoring.name}: side {entry.side} has no announcer set.");
                    }

                    voices.Add(new AnnouncerVoice { Side = entry.side, Set = entry.set });
                }
            }
        }
    }
}
