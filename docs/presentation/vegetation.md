# Vegetation

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Vegetation

Row crops and grass: plants placed and drawn on the GPU. For game developers adding a crop or another scattered thing.
Wind, trample and fire that act on plants are in [environment](environment.md).

## Row crops

Fields of crops in rows, placed and drawn on the GPU, so no crop is an object. The map tools add a `RowCropRenderer` to
each map scene and bake its `RowCropFields`: which field covers each metre, and each field's crop, row direction and
tramlines.

A `RowCrop` asset describes one crop: model variants with full and simple meshes (facing +Z, origin at the root), a
material, spacing, random scale, turn and lean, and two LOD distances. Its fields are tooltipped. Two gotchas:

- Plants cost their triangles in every pass, so `thinStart`, `farShare` and `farScale` are the lever on far fields.
- `height` shades roots toward `_CanopyShade`, which is the crop's ambient occlusion, so keep `_ScreenOcclusion` low.

Trampled plants bend or vanish, and none grow under a building footprint.

## Scatter host

Row crops and grass share one host, built to take more scattered things. A new kind subclasses these bases:

- `ScatterRenderer<TBatch>` (`RowCropRenderer`, `GrassRenderer`) culls tiles per camera and regrows when the view changes.
  A subclass supplies its inputs and batches.
- `ScatterField` holds the baked area and tiles.
- `ScatterBatch` holds one camera's draw slots (`ScatterSlots`) and queued jobs.
- `ScatterComputeInputs` finds the grow kernels and binds the heightmap, building mask, crater depth and ash.

`Scatter.hlsl` holds the job struct, hashes and slot counting. Density and distance come from `PresentationQuality`; a
density of 0 draws nothing.

## Foliage lighting

`HyperRTS/Foliage` and `FoliageGrass` are lit by the sun, Forward+ lights and Surface Cache GI, with sunlight passing
through leaves. Set `_Wrap` to 0 for plain Lambert, for pebbles and mounds scattered like grass. `_MipBias` (0 to -1)
sharpens thin stalks. Shadow, depth and motion vector passes share `DisplacedPasses.hlsl`
(see [rendering](rendering.md#shaders)).
