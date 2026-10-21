using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Pads under props: a squared yard reaches its corners, a round one leaves them to the field.</summary>
    public class MapBasePlannerTests
    {
        private static readonly Vector2 Prop = new(20f, -100f);

        /// <summary>A 6 m prop with a 3 m pad margin and no edge wander, so the pad spans 12 m.</summary>
        private static MapPlanContext Context(bool round)
        {
            var context = MapRoadFixture.Context(MapRoadFixture.Spec(), MapRoadFixture.Climate(), new MapIssues());
            var entry = new MapProp
            {
                key = "Dump", size = new Vector2(6f, 6f), pad = "Dirt", padMargin = 3f, padWander = 0f,
                padRound = round,
            };
            context.Placements.All.Add(new MapPlacement(entry, Prop, 0f, 1f));
            return context;
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ThePadCorner_IsPaddedOnlyWhenSquared(bool round, bool cornerPadded)
        {
            var context = Context(round);

            new MapBasePlanner(context).Plan();

            Assert.AreEqual(MapLandKind.Pad, context.Land.KindAt(Prop));
            Assert.AreEqual(MapLandKind.Pad, context.Land.KindAt(Prop + new Vector2(5.5f, 0f)), "edge midpoint");
            Assert.AreEqual(cornerPadded, context.Land.KindAt(Prop + new Vector2(5.5f, 5.5f)) == MapLandKind.Pad);
        }
    }
}
