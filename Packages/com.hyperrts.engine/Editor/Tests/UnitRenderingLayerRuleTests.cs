using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class UnitRenderingLayerRuleTests : TemplateFixture
    {
        [Test]
        public void TemplateUnitCarriesTheUnitsLayer()
        {
            var unit = Track(EntityTemplates.Unit());

            Assert.IsEmpty(UnitRenderingLayer.Missing(unit));
        }

        [Test]
        public void UnitWithoutTheUnitsLayerIsReportedAndFixed()
        {
            var unit = Track(EntityTemplates.Unit());
            unit.GetComponentInChildren<Renderer>().renderingLayerMask = 1u;

            var issue = ProjectValidator.Check(unit.GetComponent<UnitAuthoring>())
                .Single(found => found.FixLabel == "Add Layer");
            issue.Fix();

            Assert.IsEmpty(UnitRenderingLayer.Missing(unit));
        }
    }
}
