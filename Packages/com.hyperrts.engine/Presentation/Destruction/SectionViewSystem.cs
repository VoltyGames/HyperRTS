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
        /// <summary>Metres around a section's box that still count as an impact on it.</summary>
        private const float ImpactMargin = 0.5f;

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
            var impacts = SeenImpacts();
            var now = (float)SystemAPI.Time.ElapsedTime;
            var copies = ModelCopies.Create();
            foreach (var (building, sections, models, looks, shown, place, entity) in SystemAPI
                         .Query<RefRO<SectionedBuilding>, DynamicBuffer<Section>, DynamicBuffer<SectionModel>,
                             DynamicBuffer<SectionLook>, RefRW<ShownSections>, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                var context = new Context
                {
                    Entity = entity, Sections = sections, Models = models, Looks = looks, Place = place.ValueRO, Now = now,
                    Copies = copies, Carried = SystemAPI.GetBuffer<SectionPart>(entity),
                };
                Refresh(context, building.ValueRO.Broken, impacts, ref shown.ValueRW);
            }

            copies.Apply(EntityManager);
        }

        private NativeList<float3> SeenImpacts()
        {
            var impacts = new NativeList<float3>(Allocator.Temp);
            if (!SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true))
            {
                return impacts;
            }

            foreach (var cue in cues)
            {
                if (cue.Slot == CueSlot.Impact && _visibility.IsSeen(cue))
                {
                    impacts.Add(cue.Position);
                }
            }

            return impacts;
        }

        private void Refresh(Context at, ulong broken, NativeList<float3> impacts, ref ShownSections shown)
        {
            var quiet = !shown.Seen;
            var changed = broken ^ shown.Broken;
            var touched = quiet ? SectionedBuilding.All(at.Models.Length) : changed;
            touched |= quiet ? 0ul : Record(at, impacts, broken);
            touched |= Finished(at, shown.Breaking);
            touched |= quiet ? 0ul : Unpropped(at, changed, broken, shown.Breaking);
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

        /// <summary>Points each standing section hit by a seen impact at its nearest hole; returns the ones that changed.</summary>
        private static ulong Record(Context at, NativeList<float3> impacts, ulong broken)
        {
            var changed = 0ul;
            if (impacts.Length == 0)
            {
                return changed;
            }

            var toBuilding = math.inverse(at.Place.Value);
            foreach (var impact in impacts)
            {
                var local = math.transform(toBuilding, impact);
                for (var i = 0; i < at.Models.Length; i++)
                {
                    if ((broken & (1ul << i)) != 0ul || at.Sections[i].Gap(local) > ImpactMargin)
                    {
                        continue;
                    }

                    var look = at.Looks[i];
                    var hole = (byte)(at.Models[i].NearestHole(local) + 1);
                    changed |= hole != look.Hole ? 1ul << i : 0ul;
                    look.Hole = hole;
                    look.Impact = local;
                    look.ImpactTime = at.Now;
                    at.Looks[i] = look;
                }
            }

            return changed;
        }

        /// <summary>Broken sections not mid-clip whose supports changed, since their stub shows only while one stands.</summary>
        private static ulong Unpropped(Context at, ulong changed, ulong broken, ulong breaking)
        {
            var touched = 0ul;
            for (var i = 0; i < at.Models.Length; i++)
            {
                var bit = 1ul << i;
                if ((broken & bit) != 0ul && (breaking & bit) == 0ul && (at.Sections[i].Supports & changed) != 0ul)
                {
                    touched |= bit;
                }
            }

            return touched;
        }

        private static ulong Finished(Context at, ulong breaking)
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

        private void Step(Context at, NativeList<Entity> parts, int index, ulong broken, bool quiet, ref ShownSections shown)
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

            shown.Breaking = phase == Phase.Breaking ? shown.Breaking | bit : shown.Breaking & ~bit;
            var settles = phase == Phase.Breaking || (isBroken && !quiet && (shown.Settled & bit) != 0ul);
            shown.Settled = settles ? shown.Settled | bit : shown.Settled & ~bit;
            phase = phase == Phase.Broken && settles ? Phase.Settled : phase;
            at.Looks[index] = look;
            var hangs = phase is Phase.Broken or Phase.Settled && at.Sections[index].Hangs(broken);
            Display(at, parts, index, look, phase, hangs);
        }

        /// <summary>Starts a seen break: its effect and debris, and the pieces' clip when it has them.</summary>
        private Phase Break(Context at, int index, ref SectionLook look, in ShownSections shown)
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

        private void Display(Context at, NativeList<Entity> parts, int index, in SectionLook look, Phase phase, bool hangs)
        {
            var model = at.Models[index];
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

            for (var i = 0; i < SectionModel.MostHoles; i++)
            {
                at.Copies.Set(at.Entity, model.Hole(i).Model, standing && look.Hole == i + 1);
            }

            // A break started its pieces playing already, so this keeps them.
            at.Copies.Set(at.Entity, model.Pieces, phase is Phase.Breaking or Phase.Settled);

            at.Copies.Set(at.Entity, model.Stub, !standing && !hangs);
            at.Copies.Set(at.Entity, model.Heap, phase == Phase.Broken);
            at.Copies.Set(at.Entity, model.Broken, !standing);
        }

        /// <summary>
        /// The building's break effect and debris from the section that broke, and a dust wave off the ground floor. Out of
        /// the local player's sight only the debris falls.
        /// </summary>
        private void Play(Context at, in ShownSections shown, in Section section)
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

        // One building's data for the frame, so the steps take one argument.
        private struct Context
        {
            public Entity Entity;
            public DynamicBuffer<Section> Sections;
            public DynamicBuffer<SectionModel> Models;
            public DynamicBuffer<SectionLook> Looks;
            public DynamicBuffer<SectionPart> Carried;
            public LocalToWorld Place;
            public float Now;
            public ModelCopies Copies;
        }
    }
}
