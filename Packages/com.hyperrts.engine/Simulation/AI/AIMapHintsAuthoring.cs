using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// The map's hints for the skirmish AI: expansion order and defence points per slot, attack lanes between enemy
    /// slots. A map builder writes it (never by hand); one per match, as a plain object in the SubScene. The AI
    /// needs it: a match with AI players on a map without it logs an error and the bots stay idle.
    /// </summary>
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Match)]
    [HelpURL(HyperRTSDocs.AI)]
    [DisallowMultipleComponent]
    public class AIMapHintsAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public class SlotHints
        {
            [Tooltip("Player slot: the faction number from 1.")]
            public int slot;

            [Tooltip("Resource node positions (x, z) by walking distance from the slot's base, main field first.")]
            public List<Vector2> expansions = new();

            [Tooltip("Where to hold the ground in front of the base (x, z), nearest the base first.")]
            public List<Vector2> defence = new();
        }

        [Serializable]
        public class LaneHints
        {
            [Tooltip("Slot whose base the lane starts at.")]
            public int from;

            [Tooltip("Slot whose base the lane leads to.")]
            public int to;

            [Tooltip("Waypoints (x, z) from the start base to the end base, neither included.")]
            public List<Vector2> waypoints = new();
        }

        public List<SlotHints> slots = new();

        public List<LaneHints> lanes = new();

        public AIMapHintsData ToData()
        {
            var data = new AIMapHintsData();
            foreach (var slot in slots)
            {
                data.AddSlot(slot.slot, slot.expansions, slot.defence);
            }

            foreach (var lane in lanes)
            {
                data.AddLane(lane.from, lane.to, lane.waypoints);
            }

            return data;
        }

        public class Baker : Baker<AIMapHintsAuthoring>
        {
            public override void Bake(AIMapHintsAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.None));
                AIMapHintsSetup.Add(ref writer, authoring.ToData());
            }
        }
    }
}
