using HyperRTS.Client.Interaction;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Cursors
{
    /// <summary>The game's animated cursors, one per <see cref="CursorKind"/>; a <see cref="CursorDisplay"/> shows them.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Rig + "Cursor Set", fileName = "CursorSet")]
    public class CursorSet : ScriptableObject
    {
        [Tooltip("Pixel size a scale of 1 asks for; larger scales ask for multiples of it.")]
        [Min(1)]
        public int baseSize = 32;

        [Tooltip("One entry per cursor kind. Default is required and stands in for any kind left out.")]
        public CursorAnimation[] animations;

        /// <summary>The animation for <paramref name="kind"/>, or Default's when the set leaves it out.</summary>
        public CursorAnimation Animation(CursorKind kind)
        {
            CursorAnimation fallback = null;
            foreach (var each in animations)
            {
                if (each.kind == kind)
                {
                    return each;
                }

                fallback = each.kind == CursorKind.Default ? each : fallback;
            }

            return fallback ?? throw new MissingReferenceException($"Cursor set '{name}' has no Default cursor.");
        }
    }
}
