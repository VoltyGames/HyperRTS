using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Settlements
{
    /// <summary>Worn paths join each plot's gate to its house and its house to the well.</summary>
    public class MapPlotPathTests
    {
        // A 1.2 m path on a 1 m grid leaves a sample point up to a cell's diagonal from the nearest worn cell.
        private const float CellSlack = 0.7f;

        private MapSettlementFixture _fixture;

        [TearDown]
        public void TearDown() => _fixture?.Dispose();

        [Test]
        public void Paths_RunFromTheGateToTheHouse_AndOnToTheWell()
        {
            _fixture = MapSettlementFixture.Plan(edit: kit => kit.pathSurface = "Path");

            var houses = _fixture.InSector("House");
            var gates = _fixture.InSector("Gate");
            Assert.Greater(houses.Count, 0);
            foreach (var house in houses)
            {
                var gate = gates.OrderBy(g => Vector2.Distance(g.Position, house.Position)).First();
                // The house front stands 4 m from its centre; check the walk up to a metre short of it.
                var walk = Vector2.Distance(gate.Position, house.Position) - 5f;
                var toHouse = (house.Position - gate.Position).normalized;
                var worn = Enumerable.Range(1, Mathf.FloorToInt(walk)).Count(m => IsPath(gate.Position + toHouse * m) || RingHasPath(gate.Position + toHouse * m, CellSlack));
                Assert.GreaterOrEqual(worn, Mathf.FloorToInt(walk) - 1, $"a path from the gate at {gate.Position} to its house");
            }

            foreach (var well in _fixture.InSector("Well"))
            {
                Assert.IsTrue(RingHasPath(well.Position, 1.5f), $"a path reaches the well at {well.Position}");
            }
        }

        private bool IsPath(Vector2 point) => _fixture.Context.Land.At(point)?.Surface == "Path";

        private bool RingHasPath(Vector2 centre, float radius)
        {
            for (var angle = 0; angle < 360; angle += 15)
            {
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                if (IsPath(centre + direction * radius))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
