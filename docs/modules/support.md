# Support modules

[Modules](../modules.md) ▸ AI, Audio, Replays, Animation

## AI

| Type | Role |
| --- | --- |
| `AIPlayer`, `AITuning`, `AIDifficultyTuning` | A computer player; think interval, wave size, ability use, income bonus, fog honesty; per difficulty |
| `AIBuildStep`, `AIPlayerSetup` | Build-order entry (prefab and count); adds `AIPlayer` to a player |
| `AIPlacement` | Ring search for a building spot around the base |
| `AIMapHintsAuthoring`, `AIMapHints` | The map's bot hints, written by map builders: per slot the resource fields by distance and defence points; attack lanes between slots |
| `AIRoutes` | Seeded, integer-only lane choice |

`SkirmishAISystem` drives each `AIPlayer` through `PlayerCommand`s, only on maps with hints
(`AIMapHintsCheckSystem` logs one error otherwise). Each think it:

1. Starts the first unmet build-order step it can afford (a step at its build limit counts as met).
2. Finishes abandoned sites and sends idle harvesters to the next field with resources.
3. Then trains and researches round-robin.
4. Uses ready abilities and powers on the nearest suitable target.
5. Once enough combat units idle, attack-moves them along a seeded lane (`AIPlayer.RouteSeed` from
   `MatchSetup.Seed`, recorded in replays) to the nearest hostile building. With `LimitedByFog` it targets only
   what its team has seen.

To replace it, disable the system and write your own `PlayerCommand`s.

## Audio

| Type | Role |
| --- | --- |
| `SoundCue` (asset) | Clips, volume and pitch ranges, max instances, priority, mixer group, 2D / 3D, distance |
| `EntitySoundsAuthoring` | Cues per `CueSlot`: Fire, Impact, Death, Ability, and owner-only Ready, Select, Move, Attack; games from `Custom` |
| `SoundRules` | `HasCue`: whether an entity has a sound for a slot |

Sounds play through [cues](#cues). Playback is on the [client](client.md#presentation).

## Effects

| Type | Role |
| --- | --- |
| `EntityEffectsAuthoring` / `EntityEffect` | Rig per `CueSlot` (Fire at a `muzzle` socket, Death, Ability; games from `Custom`) |
| `ImpactEffectsAuthoring` / `EntityImpactEffects` | A weapon's `SurfaceEffects` (rig per `SurfaceMaterial`) where its shots land |
| `HitMaterialAuthoring` / `HitMaterial` | What a unit, building or tree is made of, for hits on it |
| `LoopEffectsAuthoring` / `LoopEffect` | Rigs that stream while shown (exhaust, smoke), gated by health and duration |
| `EffectTrailAuthoring` / `EffectTrail` | A rig along a projectile's path |
| `MotionDustAuthoring` / `MotionDust` | Dust by ground material and speed, from a socket |
| `EffectRules` | `TryGetSocket`: an entity's rig socket for a slot |

Sockets bake as positions in the root's local space; `EffectSetup` writes all of it and skips unset rigs. Rigs play
through [cues](#cues), on the client.

## Cues

| Type | Role |
| --- | --- |
| `CueSlot` (Common) | What a sound and effect are for; voice slots reach only the owner, Impact is sound only |
| `CueWriter`, `CueQueue` / `CueEvent` | This frame's cues (type, slot, owner, position, direction, stealth); one event when the source has a sound or an effect for the slot, at the effect socket when it has a rig |
| `CueRules` | `IsVisible` / `IsSeenLocally`: voices reach their owner, other cues whoever sees the source there, stealth honoured |

`WeaponFireSystem` plays Fire facing the target (and Impact for instant hits), `ProjectileSystem` Impact,
`DeathCueSystem` Death, `AbilityCueSystem` Ability at the aim point and `ReadyCueSystem` Ready; games append before
the lifecycle phase ends. `AcknowledgementSystem` plays a selected or commanded unit's voice at most once a second.
`CueClearSystem` empties the queue each frame. Sound and effect playback each look up their own table and skip misses.

## Replays

| Type | Role |
| --- | --- |
| `ReplayRecorder` | `Start`, `Snapshot`, `Stop` in the authoritative world |
| `Replay`, `ReplaySerializer` | Header (scene, rate, duration, players, result) and samples; GZip file |
| `ReplayViewer`, `ReplayPlayback` | Plays a replay in a local world; HUD singleton (time, speed; writing the time seeks) |

Replays store observed state, not commands: every entity with `EntityInfo` + `Faction` at 10 Hz, keyframes every
10 s. Playback stops gameplay, reveals the fog, matches recorded entities and interpolates transforms.

## Animation

Map machines (pumpjacks, turbines): `MachineAuthoring` / `MachineClock` on the root (optionally only while owned)
and `MachinePartAuthoring` on moving children: `Spin`, `Swing` in step with the spin, or `Link` (a connecting rod
between two transforms). Presentation's `MachineSystem` poses them from the clock.
