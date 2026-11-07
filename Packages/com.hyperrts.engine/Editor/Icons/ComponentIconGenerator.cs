using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Generators;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Icons
{
    /// <summary>
    /// Draws each authoring component's white glyph (<c>Components/Source/&lt;Component&gt;.svg</c>) centred on its
    /// module's coloured tile, into the PNG that <see cref="Simulation.Common.Authoring.HyperRTSIcons"/> names.
    /// </summary>
    public sealed class ComponentIconGenerator : Generator
    {
        private const string Folder = "Packages/com.hyperrts.engine/Editor/Icons/Components";
        private const string SourceFolder = Folder + "/Source";

        private const int Size = 64;

        // The glyph's side inside the tile, in pixels.
        private const float GlyphSize = 38f;

        // A module's tile colour and the components drawn on it.
        private static readonly (Color Tile, string[] Components)[] Modules =
        {
        // Animation
        (new Color(0.55f, 0.85f, 0.45f), new[]
        {
            "BoneAnimatedAuthoring", "MachineAuthoring", "MachinePartAuthoring", "MotionRigAuthoring",
            "SquadFiguresAuthoring", "TrackMeshAuthoring", "VATAnimatedAuthoring",
        }),
        // Audio
        (new Color(0.90f, 0.55f, 0.25f), new[]
        {
            "AnnouncerAuthoring", "EntityLoopsAuthoring", "EntitySoundsAuthoring", "SoundListener",
        }),
        // Buildings
        (new Color(0.45f, 0.50f, 0.58f), new[]
        {
            "AIFieldWorkAuthoring", "AirfieldAuthoring", "BuildLimitAuthoring", "BuilderAuthoring",
            "BuildingAuthoring", "CapturableAuthoring", "ConstructionSiteAuthoring", "FlagMountAuthoring",
            "GarrisonPartAuthoring", "ModelVariantsAuthoring", "ProducerAuthoring", "TeamStripAuthoring",
        }),
        // Cameras
        (new Color(0.55f, 0.40f, 0.85f), new[]
        {
            "CameraController",
        }),
        // Combat
        (new Color(0.85f, 0.25f, 0.22f), new[]
        {
            "AbilityAuthoring", "ArmorAuthoring", "AuraAuthoring", "CoverSpotAuthoring", "DamageStagesAuthoring",
            "DeathRemainsAuthoring", "DecayAuthoring", "DeployableAuthoring", "DestructibleAuthoring",
            "ImpactMarksAuthoring", "ReinforcePointAuthoring", "RetreatPointAuthoring", "SectionsAuthoring",
            "SpawnEffectAuthoring", "StatusEffectAuthoring", "StrategicAssetAuthoring", "StretchPartsAuthoring",
            "ToppleAuthoring", "TossedTurretAuthoring", "TurretTossAuthoring", "VeterancyAuthoring",
            "WeaponAuthoring",
        }),
        // Effects
        (new Color(1.00f, 0.42f, 0.10f), new[]
        {
            "DebrisAuthoring", "EffectLight", "EffectRig", "EffectTrailAuthoring", "EntityEffectsAuthoring",
            "GroundMaterialsAuthoring", "HitMaterialAuthoring", "ImpactEffectsAuthoring", "LoopEffectsAuthoring",
            "LoopLight", "MotionDustAuthoring", "RotorWashAuthoring", "SmokeShadow",
        }),
        // Environment
        (new Color(0.62f, 0.45f, 0.28f), new[]
        {
            "AmbientLife", "Atmosphere", "CloudShadows", "FieldFireSettings", "GrassField", "GrassRenderer",
            "GroundColor", "Horizon", "RowCrop", "RowCropFields", "RowCropRenderer", "TrampleMap", "TrenchAuthoring",
            "WeatherSet", "Wind",
        }),
        // Match
        (new Color(0.30f, 0.75f, 0.35f), new[]
        {
            "AIMapHintsAuthoring", "MatchAuthoring", "MatchStateAuthoring",
        }),
        // Navigation
        (new Color(0.20f, 0.75f, 0.70f), new[]
        {
            "BlockerAuthoring", "DockAuthoring", "NavAreaAuthoring", "NavObstacleAuthoring",
            "TerrainHeightAuthoring",
        }),
        // Research
        (new Color(0.50f, 0.58f, 0.72f), new[]
        {
            "UpgradeAuthoring",
        }),
        // Resources
        (new Color(0.95f, 0.70f, 0.20f), new[]
        {
            "AirdropReceiverAuthoring", "CasualtyIncomeAuthoring", "DeliverySourceAuthoring", "HarvesterAuthoring",
            "ReserveRefillAuthoring", "ResourceDropOffAuthoring", "ResourceIncomeAuthoring", "ResourceNodeAuthoring",
            "ResourceStagesAuthoring",
        }),
        // Selection
        (new Color(0.30f, 0.70f, 1.00f), new[]
        {
            "PickBoxAuthoring", "SelectionDragBoxUI",
        }),
        // UI
        (new Color(0.85f, 0.45f, 0.75f), new[]
        {
            "HUDController", "MinimapImage", "OverlayRenderer",
        }),
        // Units
        (new Color(0.25f, 0.50f, 0.90f), new[]
        {
            "CapturerAuthoring", "ContainerAuthoring", "CrusherAuthoring", "FlightAuthoring", "GrimeAuthoring",
            "LocomotionAuthoring", "PassengerAuthoring", "SquadAuthoring", "TrackMarksAuthoring", "UnitAuthoring",
        }),
        // Vision
        (new Color(0.60f, 0.60f, 0.25f), new[]
        {
            "CropCoverAuthoring", "DetectorAuthoring", "FogOfWarRenderer", "SightBlockAuthoring", "StealthAuthoring",
        }),
        };

        public override string Name => "Component icons";

        public override string Group => "HyperRTS";

        public override int Order => 10;

        public override string Description => "One icon per authoring component: its glyph on the module's colour.";

        public override string Sources => "Glyphs in Components/Source and the module colours in ComponentIconGenerator.cs";

        public override IEnumerable<string> Inputs => new[] { SourceFolder, ScriptPath() };

        public override string Outputs => "One PNG per component in Packages/com.hyperrts.engine/Editor/Icons/Components/";

        public override bool Available => EnginePackage.Writable;

        public override void Run()
        {
            var unlisted = Directory.GetFiles(SourceFolder, "*.svg")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !IsListed(name))
                .ToList();
            if (unlisted.Count > 0)
            {
                throw new InvalidOperationException($"Component icons: no module lists {string.Join(", ", unlisted)}.");
            }

            foreach (var (tile, components) in Modules)
            {
                foreach (var component in components)
                {
                    WriteIcon($"{SourceFolder}/{component}.svg", tile, $"{Folder}/{component}.png");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Generated {Modules.Sum(module => module.Components.Length)} component icons.");
        }

        /// <summary>Whether a module tile lists <paramref name="component"/>.</summary>
        public static bool IsListed(string component) => Modules.Any(module => module.Components.Contains(component));

        /// <summary>Draws the SVG's white glyph on a rounded tile and writes it, imported as a GUI texture, to <paramref name="pngPath"/>.</summary>
        public static void WriteIcon(string svgPath, Color tile, string pngPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, Render(svgPath, tile).EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings(pngPath);
        }

        private static Texture2D Render(string svgPath, Color tile)
        {
            var glyph = new GlyphRaster(svgPath, Size, GlyphSize);
            const float margin = 6f;
            const float radius = 14f;
            const float half = Size / 2f - margin;
            const float center = (Size - 1) / 2f;
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var alpha = Mathf.Clamp01(0.5f - RoundedBoxSdf(x - center, y - center, half, half, radius));
                    var color = Color.Lerp(tile, Color.white, glyph.Coverage(x, y));
                    pixels[y * Size + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float RoundedBoxSdf(float px, float py, float halfW, float halfH, float r)
        {
            var qx = Mathf.Abs(px) - halfW + r;
            var qy = Mathf.Abs(py) - halfH + r;
            var outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            var inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        private static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.GUI;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
