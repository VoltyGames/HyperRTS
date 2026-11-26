using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows each <see cref="Section"/> standing, breaking or broken to match the building's broken mask. Breaks this
    /// client saw play their clip, effect and debris; a loaded or unseen building shows the end state. The intact model and
    /// the section's parts hide in place; the others are copied in from their prefabs while they show.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class SectionViewSystem : SystemBase
    {
        private enum Phase
        {
            Standing,
            Breaking,
            Broken,

            // Broken after a seen break: its pieces stay where the clip left them, so nothing jumps to the heap.
            Settled,
        }

        private EffectPool _pool;
        private DebrisSystem _debris;
        private RenderSwitch _switch;
        private CueVisibility _visibility;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _debris = World.GetOrCreateSystemManaged<DebrisSystem>();
            _switch = new RenderSwitch(ref CheckedStateRef);
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<ShownSections>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _switch.Update(ref CheckedStateRef);
            _visibility.Begin();
            var impacts = new SectionImpacts { Seen = SeenImpacts() };
            var now = (float)SystemAPI.Time.ElapsedTime;
            var copies = ModelCopies.Create();
            foreach (var (building, sections, models, looks, carried, shown, place, entity) in SystemAPI
                         .Query<RefRO<SectionedBuilding>, DynamicBuffer<Section>, DynamicBuffer<SectionModel>,
                             DynamicBuffer<SectionLook>, DynamicBuffer<SectionPart>, RefRW<ShownSections>,
                             RefRO<LocalToWorld>>().WithEntityAccess())
            {
                var context = new SectionViewContext
                {
                    Entity = entity, Sections = sections, Models = models, Looks = looks, Place = place.ValueRO, Now = now,
                    Copies = copies, Carried = carried, Holes = new SectionHoles(EntityManager.GetBuffer<SectionHole>(entity, true)),
                };
                Refresh(context, building.ValueRO.Broken, impacts, ref shown.ValueRW);
            }

            copies.Apply(EntityManager);
        }

        private NativeList<CueEvent> SeenImpacts()
        {
            var impacts = new NativeList<CueEvent>(Allocator.Temp);
            if (!SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true))
            {
                return impacts;
            }

            foreach (var cue in cues)
            {
                if (cue.Slot == CueSlot.Impact && _visibility.IsSeen(cue))
                {
                    impacts.Add(cue);
                }
            }

            return impacts;
        }

        private void Refresh(SectionViewContext at, ulong broken, in SectionImpacts impacts, ref ShownSections shown)
        {
            var quiet = !shown.Seen;
            var changed = broken ^ shown.Broken;
            var touched = quiet ? SectionedBuilding.All(at.Models.Length) : changed;
            touched |= quiet ? 0ul : impacts.Record(at, broken, shown.Broken);
            touched |= Finished(at, shown.Breaking);
            touched |= quiet ? 0ul : Unpropped(at, changed, broken);
            if (touched != 0ul)
            {
                var parts = new NativeList<Entity>(Allocator.Temp);
                _switch.Collect(at.Entity, parts);
                for (var i = 0; i < at.Models.Length; i++)
                {
                    if ((touched & (1ul << i)) != 0ul)
                    {
                        Step(at, parts, i, broken, quiet, ref shown);
                    }
                }
            }

            shown.Broken = broken;
            shown.Seen = true;
        }

        /// <summary>Broken sections whose supports or rests changed, which gate their stub and debris.</summary>
        private static ulong Unpropped(SectionViewContext at, ulong changed, ulong broken)
        {
            var touched = 0ul;
            for (var i = 0; i < at.Models.Length; i++)
            {
                var bit = 1ul << i;
                if ((broken & bit) != 0ul && ((at.Sections[i].Holders | at.Sections[i].Rests) & changed) != 0ul)
                {
                    touched |= bit;
                }
            }

            return touched;
        }

        private static ulong Finished(SectionViewContext at, ulong breaking)
        {
            var done = 0ul;
            for (var i = 0; i < at.Models.Length; i++)
            {
                var bit = 1ul << i;
                if ((breaking & bit) != 0ul && at.Now >= at.Looks[i].BreakEnd)
                {
                    done |= bit;
                }
            }

            return done;
        }

        private void Step(SectionViewContext at, NativeList<Entity> parts, int index, ulong broken, bool quiet, ref ShownSections shown)
        {
            var bit = 1ul << index;
            var isBroken = (broken & bit) != 0ul;
            var wasBroken = (shown.Broken & bit) != 0ul;
            var look = at.Looks[index];
            var phase = isBroken ? Phase.Broken : Phase.Standing;
            if (quiet)
            {
                look = SectionLook.Untouched;
            }
            else if (isBroken && !wasBroken)
            {
                phase = Break(at, index, ref look, shown);
            }
            else if (!isBroken && wasBroken)
            {
                look = SectionLook.Untouched;
            }
            else if ((shown.Breaking & bit) != 0ul && at.Now < look.BreakEnd)
            {
                // Its supports or rests changed mid-clip: the pieces play on.
                phase = Phase.Breaking;
            }

            shown.Breaking = phase == Phase.Breaking ? shown.Breaking | bit : shown.Breaking & ~bit;
            var settles = phase == Phase.Breaking || (isBroken && !quiet && (shown.Settled & bit) != 0ul);
            shown.Settled = settles ? shown.Settled | bit : shown.Settled & ~bit;
            phase = phase == Phase.Broken && settles ? Phase.Settled : phase;
            at.Looks[index] = look;
            Display(at, parts, index, look, phase, broken);
        }

        /// <summary>Starts a seen break: its effect and debris, and the pieces' clip when it has them.</summary>
        private Phase Break(SectionViewContext at, int index, ref SectionLook look, in ShownSections shown)
        {
            Play(at, shown, at.Sections[index]);
            if (!at.Models[index].HasPieces || !SystemAPI.HasComponent<SectionClips>(at.Entity))
            {
                return Phase.Broken;
            }

            var clip = SystemAPI.GetComponent<SectionClips>(at.Entity).Pick(at.Sections[index], look, at.Now);
            var range = new ClipRange { StartRow = clip.StartRow, FrameCount = clip.FrameCount, Fps = clip.Fps };
            at.Copies.Play(at.Entity, at.Models[index].Pieces, PlayingClip.Started(range, at.Now));
            look.BreakEnd = at.Now + clip.Seconds;
            return Phase.Breaking;
        }

        private void Display(SectionViewContext at, NativeList<Entity> parts, int index, in SectionLook look, Phase phase, ulong broken)
        {
            var model = at.Models[index];
            // A section that fell because its supports went has none from its first frame: its stub never hangs.
            var hangs = at.Sections[index].Hangs(broken);
            var lost = at.Sections[index].DebrisHangs(broken);
            var standing = phase == Phase.Standing;
            var hidden = !standing || look.Hole != 0;
            _switch.SetBranch(parts, model.Intact, HideReason.Section, hidden);
            foreach (var carried in at.Carried)
            {
                if (carried.Section == index)
                {
                    _switch.SetBranch(parts, carried.Part, HideReason.Section, hidden);
                }
            }

            var number = 0;
            foreach (var hole in at.Holes.All)
            {
                if (hole.Section == index)
                {
                    at.Copies.Set(at.Entity, hole.Model, standing && look.Hole == ++number);
                }
            }

            // A break started its pieces playing already, so this keeps them. Debris goes down with what it lay on.
            at.Copies.Set(at.Entity, model.Pieces, phase is Phase.Breaking or Phase.Settled && !lost);
            at.Copies.Set(at.Entity, model.Stub, !standing && !hangs);
            at.Copies.Set(at.Entity, model.Heap, phase == Phase.Broken && !lost);
            at.Copies.Set(at.Entity, model.Broken, !standing);
        }

        /// <summary>
        /// The building's break effect and debris from the section that broke, and a dust wave off the ground floor. Out of
        /// the local player's sight only the debris falls.
        /// </summary>
        private void Play(SectionViewContext at, in ShownSections shown, in Section section)
        {
            var position = math.transform(at.Place.Value, section.Center);
            var outward = section.Outward;
            var seen = _visibility.IsSeen(position);
            if (seen && shown.Effect.IsValid())
            {
                var up = math.abs(outward.y) > 0.5f ? new float3(0f, 0f, 1f) : math.up();
                _pool.Play(shown.Effect.Value, position, math.mul(at.Place.Rotation, quaternion.LookRotation(outward, up)));
            }

            if (seen && shown.DustWave.IsValid() && section.Floor == 0)
            {
                var foot = math.transform(at.Place.Value, section.Center - new float3(0f, section.HalfSize.y, 0f));
                _pool.Play(shown.DustWave.Value, foot, at.Place.Rotation);
            }

            if (SystemAPI.HasComponent<Debris>(at.Entity))
            {
                var debris = SystemAPI.GetComponent<Debris>(at.Entity);
                _debris.Throw(debris.Set.Value, position, math.rotate(at.Place.Rotation, outward), debris.Radius, 0);
            }
        }
    }
}
