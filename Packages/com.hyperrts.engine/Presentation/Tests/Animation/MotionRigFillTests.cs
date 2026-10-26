using System.Linq;
using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation
{
    /// <summary>The motion rig authoring's fill from a model: parts found by name, weapons wired, steering axles, tuned slots kept.</summary>
    public class MotionRigFillTests
    {
        private GameObject _unit;

        [SetUp]
        public void SetUp() => _unit = new GameObject("Unit");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_unit);

        [Test]
        public void FillFromModel_FindsThePartsByName()
        {
            var model = Pivot("Model", _unit.transform);
            var hull = Pivot("Hull", model, new Vector3(3f, 2f, 7f));
            var turret = Pivot("Turret", hull);
            var barrel = Pivot("Barrel", turret);
            var launcher = Pivot("Launcher", turret);
            var wheel = Pivot("Wheel_L01", hull, new Vector3(0.3f, 0.8f, 0.8f));
            Pivot("Sprocket_R", hull, Vector3.one);
            Pivot("Muzzle_01", barrel);
            var weapon = _unit.AddComponent<WeaponAuthoring>();
            weapon.extraWeapons.Add(new WeaponAuthoring.WeaponSlot());
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(turret, rig.turrets.Single().part);
            Assert.AreEqual(new[] { barrel, launcher }, rig.elevations.Select(slot => slot.part).ToArray());
            Assert.AreEqual(new[] { 0, 1 }, rig.elevations.Select(slot => slot.weapon).ToArray(), "launcher on the mount");
            Assert.AreEqual(barrel, rig.recoils.Single().part);
            Assert.AreEqual(2, rig.wheels.Count);
            Assert.AreEqual(0.4f, rig.wheels.Single(slot => slot.part == wheel).radius, 1e-4f);
            Assert.IsEmpty(rig.rotors);
            Assert.AreEqual(hull, rig.tiltRoot);
            Assert.AreEqual(new Vector2(7f, 3f), rig.footprint);
            Assert.AreEqual((turret, barrel), (weapon.stats.turret, weapon.stats.barrel), "the gun's pivots");
            var mount = weapon.extraWeapons[0].stats;
            Assert.AreEqual((turret, launcher), (mount.turret, mount.barrel), "the launcher turns with the turret it sits on");
            CollectionAssert.IsEmpty(rig.Problems());
        }

        [Test]
        public void FillFromModel_PosesTheDamagedModelLikeTheIntactOne()
        {
            var model = Pivot("Model", _unit.transform);
            var hull = Pivot("Hull", model, new Vector3(3f, 2f, 7f));
            var turret = Pivot("Turret", hull);
            var damaged = Pivot("Damaged", _unit.transform);
            var damagedHull = Pivot("Hull", damaged, new Vector3(3f, 2f, 7f));
            var damagedTurret = Pivot("Turret", damagedHull);
            _unit.AddComponent<DamageStagesAuthoring>().stages = new()
            {
                new() { below = 1f, visual = model.gameObject },
                new() { below = 0.5f, visual = damaged.gameObject },
            };
            _unit.AddComponent<WeaponAuthoring>().turreted = true;
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(new[] { turret, damagedTurret }, rig.turrets.Select(slot => slot.part).ToArray());
            Assert.AreEqual((hull, damagedHull), (rig.tiltRoot, rig.tiltFollower));
            CollectionAssert.IsEmpty(rig.Problems());
        }

        [Test]
        public void FillFromModel_LeavesMachinePartsToTheirMachine()
        {
            var turret = Pivot("Turret", Pivot("Hull", _unit.transform, Vector3.one));
            var box = Pivot("Barrel", turret);
            box.gameObject.AddComponent<HyperRTS.Simulation.Animation.MachinePartAuthoring>();
            var weapon = _unit.AddComponent<WeaponAuthoring>();
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.IsEmpty(rig.elevations, "the machine raises the box");
            Assert.AreEqual((turret, box), (weapon.stats.turret, weapon.stats.barrel), "shots still leave the raised box");
        }

        [Test]
        public void FillFromModel_AgainKeepsTunedSlots()
        {
            var barrel = Pivot("Barrel", Pivot("Hull", _unit.transform, Vector3.one));
            _unit.AddComponent<WeaponAuthoring>();
            var rig = _unit.AddComponent<MotionRigAuthoring>();
            rig.FillFromModel();
            rig.recoils[0].distance = 0.8f;

            rig.FillFromModel();

            Assert.AreEqual(barrel, rig.recoils.Single().part);
            Assert.AreEqual(0.8f, rig.recoils[0].distance);
        }

        [Test]
        public void FillFromModel_SpinsPropellersAboutTheNose()
        {
            var hull = Pivot("Hull", _unit.transform, new Vector3(4f, 3f, 30f));
            var props = new[] { Pivot("Prop_01", hull), Pivot("Prop_02", hull) };
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(props, rig.rotors.Select(slot => slot.part).ToArray());
            Assert.IsTrue(rig.rotors.All(slot => slot.axis == Vector3.forward));
        }

        [Test]
        public void FillFromModel_TiltsTheModelWhenTheHullIsItsRoot()
        {
            var model = Pivot("Model", _unit.transform, new Vector3(3f, 2f, 7f));
            Pivot("Turret", model);
            _unit.AddComponent<WeaponAuthoring>();
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(model, rig.tiltRoot);
            Assert.AreEqual(new Vector2(7f, 3f), rig.footprint);
        }

        [Test]
        public void FillFromModel_OnABuilding_AimsTheInterceptorAfterTheWeapon_AndNeverTilts()
        {
            var turret = Pivot("Turret", Pivot("Hull", _unit.transform, Vector3.one));
            var launcher = Pivot("Launcher", turret);
            _unit.AddComponent<BuildingAuthoring>();
            _unit.AddComponent<WeaponAuthoring>();
            var interceptor = _unit.AddComponent<InterceptorAuthoring>();
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(1, interceptor.Pose);
            Assert.AreEqual(1, rig.elevations.Single(slot => slot.part == launcher).weapon, "the launcher follows it");
            Assert.AreEqual((turret, launcher), (interceptor.turret, interceptor.barrel));
            Assert.IsNull(rig.tiltRoot);
            CollectionAssert.IsEmpty(rig.Problems());

            rig.tiltRoot = turret;
            StringAssert.Contains("never tilts", rig.Problems().Single());
        }

        [Test]
        public void FillFromModel_GivesNumberedTurretsAndLaunchersTheirOwnWeapon()
        {
            var first = Pivot("Turret_01", _unit.transform, Vector3.one);
            var firstLauncher = Pivot("Launcher_01", first);
            var second = Pivot("Turret_02", _unit.transform, Vector3.one);
            var secondLauncher = Pivot("Launcher_02", second);
            _unit.AddComponent<BuildingAuthoring>();
            var weapon = _unit.AddComponent<WeaponAuthoring>();
            var interceptor = _unit.AddComponent<InterceptorAuthoring>();
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(new[] { 0, 1 }, rig.turrets.Select(slot => slot.weapon).ToArray());
            Assert.AreEqual(new[] { 0, 1 }, rig.elevations.Select(slot => slot.weapon).ToArray());
            Assert.AreEqual((first, firstLauncher), (weapon.stats.turret, weapon.stats.barrel), "the weapon's pivots");
            Assert.AreEqual((second, secondLauncher), (interceptor.turret, interceptor.barrel), "the interceptor's pivots");
            CollectionAssert.IsEmpty(rig.Problems());
        }

        [Test]
        public void Problems_NameAPoseTheUnitLacks()
        {
            Pivot("Turret", _unit.transform, Vector3.one);
            var rig = _unit.AddComponent<MotionRigAuthoring>();
            rig.FillFromModel();

            StringAssert.Contains("follows weapon 0", rig.Problems().Single());
        }

        [Test]
        public void FillFromModel_TiltsAModelWithNoMovingParts()
        {
            var model = Pivot("Model", _unit.transform);
            Pivot("Exhaust_01", model);
            var hull = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            hull.name = "Hull_LOD0";
            hull.SetParent(model, false);
            hull.localScale = new Vector3(50f, 3f, 20f);
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            rig.FillFromModel();

            Assert.AreEqual(model, rig.tiltRoot);
            Assert.AreEqual(new Vector2(20f, 50f), rig.footprint);
        }

        [Test]
        public void FillFromWheels_MeasuresTheChassis_AndTheRigSteersOnlyTheFrontAxle()
        {
            var hull = Pivot("Hull", Pivot("Model", _unit.transform), new Vector3(2.5f, 2f, 7f));
            var steered = new[] { Wheel("Wheel_L01", hull, -1f, 2.5f), Wheel("Wheel_R01", hull, 1f, 2.5f) };
            Wheel("Wheel_L02", hull, -1f, -1f);
            Wheel("Wheel_R02", hull, 1f, -1f);
            Wheel("Wheel_L03", hull, -1f, -2.4f);
            var locomotion = _unit.AddComponent<LocomotionAuthoring>();
            locomotion.pivots = false;
            locomotion.turnRadius = 9f;
            var rig = _unit.AddComponent<MotionRigAuthoring>();

            locomotion.FillFromWheels();
            rig.FillFromModel();

            Assert.AreEqual(4.2f, locomotion.wheelbase, 1e-4f, "front axle to the rear pair's midpoint");
            Assert.AreEqual(1.7f, locomotion.rearAxle, 1e-4f, "rear pair's midpoint behind the centre");
            CollectionAssert.AreEquivalent(steered, rig.wheels.Where(slot => slot.steers).Select(slot => slot.part));
            CollectionAssert.IsEmpty(rig.Problems());
        }

        // A pivot with a cube mesh child of the given size, or an empty one.
        private static Transform Pivot(string name, Transform parent, Vector3 meshSize = default)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            if (meshSize != default)
            {
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                mesh.name = name + "_LOD0";
                mesh.SetParent(pivot, false);
                mesh.localScale = meshSize;
            }

            return pivot;
        }

        private static Transform Wheel(string name, Transform hull, float x, float z)
        {
            var wheel = Pivot(name, hull, new Vector3(0.3f, 0.8f, 0.8f));
            wheel.localPosition = new Vector3(x, 0f, z);
            return wheel;
        }
    }
}
