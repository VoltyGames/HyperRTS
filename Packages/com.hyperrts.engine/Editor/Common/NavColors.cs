using HyperRTS.Simulation.Navigation;
using UnityEngine;

namespace HyperRTS.Editor.Common
{
    /// <summary>Nav surface colours, shared by the nav area handles and the Play-mode nav grid layer.</summary>
    public static class NavColors
    {
        public static readonly Color Blocked = new(1f, 0.2f, 0.2f);
        public static readonly Color Water = new(0.2f, 0.45f, 1f);
        public static readonly Color Deck = new(1f, 0.8f, 0.2f);

        public static Color Of(NavAreaKind kind)
        {
            switch (kind)
            {
                case NavAreaKind.Water:
                    return Water;
                case NavAreaKind.Blocked:
                    return Blocked;
                default:
                    return Deck;
            }
        }
    }
}
