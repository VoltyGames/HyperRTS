using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How a climate farms its land: field sizes on the grid, the crop mix, weedy margins, shelterbelts across the
    /// field ends and the dirt tracks beside them.
    /// </summary>
    [Serializable]
    public sealed class MapFarmlandKit
    {
        public string key;

        [Tooltip("Field length along the rows (x) and width across them (y), in metres.")]
        public Vector2 fieldSize = new(160f, 80f);

        public List<MapCropShare> crops = new();

        [Tooltip("Weedy strip round each field (metres).")]
        public float marginWidth = 2.5f;

        public string marginSurface;

        [Tooltip("Belt kit planted across the field ends; empty for none.")]
        public string beltKit;

        [Tooltip("A belt on every Nth line of field ends.")]
        [Min(1)]
        public int beltEvery = 1;

        [Tooltip("Road class of the tracks along the field ends; empty for none.")]
        public string trackClass;

        [Tooltip("Field fragments smaller than this (m²) become margin ground.")]
        public float minField = 1500f;
    }
}
