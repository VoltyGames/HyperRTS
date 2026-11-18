// The _Stain* material fields TerrainStains.hlsl reads. Include inside the shader's UnityPerMaterial CBUFFER, last, so
// the Terrain and Road shaders share one layout.
float _StainRubbleTile;
half _StainRubbleSmoothness;
float _StainAshTile;
half _StainAshSmoothness;
half4 _StainCharColor;
float _StainMudTile;
half _StainMudSmoothness;
float _StainCraterTile;
half _StainCraterSmoothness;
