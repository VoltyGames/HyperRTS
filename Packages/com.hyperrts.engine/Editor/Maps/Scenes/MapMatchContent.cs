using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>The Match's sides and player slots from the palette and spawns, so a map played as baked has kits and sides.</summary>
    public static class MapMatchContent
    {
        public static List<SideContent> Sides(MapPalette palette) => palette.sides.Select(side => new SideContent
        {
            side = (byte)side.side,
            startKit = side.startKit.Select(item => new StartKitItem
            {
                prefab = palette.Entry(item.key).prefab,
                offset = item.offset,
                yaw = item.yaw,
                facesCamera = item.facesCamera,
            }).ToList(),
            buildOrder = side.buildOrder,
        }).ToList();

        /// <summary>Slot n plays side <c>sides[(n - 1) % count]</c>; <paramref name="ground"/> is the world height at a point.</summary>
        public static List<PlayerSlot> Players(IReadOnlyList<MapSpawn> spawns, MapPalette palette,
            Func<Vector2, float> ground)
        {
            var sides = palette.sides;
            return spawns.Select(spawn => new PlayerSlot
            {
                name = spawn.Slot == 1 ? "Player" : $"AI {spawn.Slot - 1}",
                team = spawn.Team,
                color = PlayerSlot.Palette[(spawn.Slot - 1) % PlayerSlot.Palette.Length],
                control = spawn.Slot == 1 ? PlayerControl.LocalHuman : PlayerControl.AI,
                startingResources = new List<ResourceQuantity>(palette.startingResources),
                side = sides.Count > 0 ? (byte)sides[(spawn.Slot - 1) % sides.Count].side : (byte)0,
                spawnPosition = new Vector3(spawn.Position.x, ground(spawn.Position), spawn.Position.y),
                spawnYaw = spawn.Yaw,
            }).ToList();
        }
    }
}
