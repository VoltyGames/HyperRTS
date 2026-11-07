# Vegetation

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Vegetation

Row crops and grass: plants placed and drawn on the GPU. For game developers adding a crop or another scattered thing.
Wind, trample and fire that act on plants are in [environment](environment.md).

## Row crops

Fields of crops in rows, placed and drawn on the GPU, so no crop is an object. The map tools add a `RowCropRenderer` to
each map scene and bake its `RowCropFields`: which field covers each metre, and each field's crop, row direction and
tramlines (pairs of wheel lines where no plant grows, which the map tools also paint into the terrain). It runs in edit
mode too.

A `RowCrop` asset describes one crop: model variants with full and simple meshes (facing +Z, origin at the root), a
material, row and plant spacing, random scale, turn and lean, lodging and two LOD distances. Notable fields:

- `stand` leaves gaps where sown spots never came up; `edgeGrowth` shrinks plants at a field's edge; `edgeMargin` keeps a
  field off ground that grows no crop or another one. Two fields of one crop meet without a gap.
- `thinStart`, `farShare` and `farScale` thin a crop with distance and grow the kept plants to cover the gaps. Plants
  cost their triangles in every pass, so this is the lever on far fields. Crops cast shadows only from bands ending
  within `PresentationQuality.CropShadowDistance` (`RowCropBands.CastsShadows`).
- `lodging` marks a share of lattice cells where plants lie flattened (`Lodging` in `RowCrops.compute`).
- `height` shades the field toward `_CanopyShade` at the roots; that term is the crop's ambient occlusion, so
  `_ScreenOcclusion` stays low to keep screen-space AO from darkening it twice. `_WindSheen` rolls a light band across the
  field with gusts.

Plants ripen in the same dry patches as the ground and grass round them. Each plant switches model at its own distance
(a jittered share of the LOD distances) so models mix instead of meeting at a ring. A camera regrows its visible tiles
when it moves, the quality changes or new ash falls, and every frame while a trample map is published
(`RowCropRenderer.RegrowsEveryFrame`; grass doesn't read that map). Trampled plants bend or vanish, and none grow under a
building footprint.

## Scatter host

Row crops and grass (see [maps](../maps/README.md)) share one host, built to take more scattered things:

- `ScatterRenderer<TBatch>`, the base of `RowCropRenderer` and `GrassRenderer`, holds the `compute` and `terrain` fields,
  keeps one `ScatterCameraState` per camera, culls tiles through `ScatterTiles`, regrows a camera when its view changed
  and, in edit mode, rebuilds after `OnValidate` or a map rebuild. A subclass makes its inputs and batches, names its
  `ScatterField`, queues each visible tile's jobs and says when its batches went stale. Density and distance come from
  `PresentationQuality.Current`.
- `ScatterField`, the base of `RowCropFields` and `GrassField`, holds the baked `area` and the `tiles` (`ScatterTile`) and
  makes the culling boxes.
- `ScatterBatch`, the base of `RowCropBatch` and `GrassBatch`, holds one camera's slots and queued `ScatterJob`s.
  `Generate` binds the jobs, grows in one dispatch and counts; `Draw` draws each slot with room.
- `ScatterSlots` is the room: one instance buffer per batch cut into a slot per draw, with a table of offsets and
  capacities that the kernels and draws read. Grass slots are (plant, model, band); a crop's are variant times two LOD
  bands.
- `ScatterComputeInputs`, the base of `GrassComputeInputs` and `RowCropComputeInputs`, finds the grow and `Count` kernels,
  uploads the baked items and binds what both read: tile size, heightmap, and per camera the building mask, crater depth
  and ash. `ScatterSettings` carries density and distances; a density of 0 draws nothing.
- `Scatter.hlsl` holds the job struct, hashes, noise, band edges and slot counting; `ScatterHash` and `ScatterBands` are
  the C# twins. `BandRange` jitters a plant's band edges by its hash.

## Foliage lighting

`HyperRTS/Foliage` and `FoliageGrass` are lit by the sun, Forward+ lights and Surface Cache GI, with sunlight passing
through leaves to their far side. `_Wrap` at 1 keeps wrapped diffuse, translucency and sheen; at 0 plants take plain
Lambert, for pebbles and mounds scattered like grass. `_MipBias` (0 to -1) samples the cutout atlas sharper; about -0.5
suits thin stalks. Shadow, depth and motion vector passes share `DisplacedPasses.hlsl`, so blades reproject under TAA
(see [rendering](rendering.md#shaders)).
