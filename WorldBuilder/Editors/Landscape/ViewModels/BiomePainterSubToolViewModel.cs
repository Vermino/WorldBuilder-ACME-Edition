using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatReaderWriter.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using WorldBuilder.Editors.Landscape.Commands;
using WorldBuilder.Lib;
using WorldBuilder.Lib.History;
using WorldBuilder.Shared.Documents;
using WorldBuilder.Shared.Models;

namespace WorldBuilder.Editors.Landscape.ViewModels {
    public partial class BiomePainterSubToolViewModel : SubToolViewModelBase {
        public override string Name => "Biome";
        public override string IconGlyph => "🌳";

        [ObservableProperty]
        private ObservableCollection<BiomeDefinition> _availableBiomes;

        [ObservableProperty]
        private BiomeDefinition? _selectedBiome;

        [ObservableProperty]
        private float _brushRadius = 20f;

        [ObservableProperty]
        private float _objectDensityMultiplier = 1.0f;

        [ObservableProperty]
        private bool _autoPlaceObjects = true;

        private bool _isPainting;
        private Vector3 _currentHitPosition;
        private readonly CommandHistory _commandHistory;
        private readonly Random _random = new();

        // Pending changes for the current stroke
        private readonly Dictionary<ushort, List<(int VertexIndex, byte OriginalType, byte NewType)>> _pendingTextureChanges = new();
        private readonly List<(ushort LandblockKey, StaticObject Object)> _pendingObjects = new();
        private readonly List<(ushort LandblockKey, int AddedIndex)> _pendingObjectIndices = new();

        public BiomePainterSubToolViewModel(
            TerrainEditingContext context,
            CommandHistory commandHistory) : base(context) {
            _commandHistory = commandHistory;

            _availableBiomes = new ObservableCollection<BiomeDefinition> {
                BiomeLibrary.Forest,
                BiomeLibrary.Desert,
                BiomeLibrary.Mountain,
                BiomeLibrary.Swamp,
                BiomeLibrary.Snow
            };

            _selectedBiome = _availableBiomes.FirstOrDefault();
        }

        partial void OnBrushRadiusChanged(float value) {
            if (value < 1f) BrushRadius = 1f;
            if (value > 200f) BrushRadius = 200f;
            if (Context.BrushActive) {
                Context.BrushRadius = BrushRadius;
            }
        }

        public override void OnActivated() {
            Context.BrushActive = true;
            Context.BrushRadius = BrushRadius;
            UpdatePreviewTexture();
            _pendingTextureChanges.Clear();
            _pendingObjects.Clear();
            _pendingObjectIndices.Clear();
        }

        public override void OnDeactivated() {
            Context.BrushActive = false;
            Context.PreviewTextureAtlasIndex = -1;
            if (_isPainting) {
                FinalizePainting();
            }
        }

        partial void OnSelectedBiomeChanged(BiomeDefinition? value) {
            UpdatePreviewTexture();
        }

        private void UpdatePreviewTexture() {
            if (_selectedBiome != null) {
                Context.PreviewTextureAtlasIndex = Context.TerrainSystem.Scene.SurfaceManager
                    .GetAtlasIndexForTerrainType(_selectedBiome.PrimaryTexture);
            }
            else {
                Context.PreviewTextureAtlasIndex = -1;
            }
        }

        public override bool HandleMouseDown(MouseState mouseState) {
            if (!mouseState.IsOverTerrain || !mouseState.TerrainHit.HasValue || !mouseState.LeftPressed || _selectedBiome == null)
                return false;

            _isPainting = true;
            _pendingTextureChanges.Clear();
            _pendingObjects.Clear();
            _pendingObjectIndices.Clear();

            ApplyBiome(mouseState.TerrainHit.Value.HitPosition);
            return true;
        }

        public override bool HandleMouseMove(MouseState mouseState) {
            if (!mouseState.IsOverTerrain || !mouseState.TerrainHit.HasValue) return false;

            _currentHitPosition = mouseState.TerrainHit.Value.HitPosition;
            Context.BrushCenter = new Vector2(_currentHitPosition.X, _currentHitPosition.Y);

            if (_isPainting) {
                ApplyBiome(_currentHitPosition);
            }

            return true;
        }

        public override bool HandleMouseUp(MouseState mouseState) {
            if (_isPainting) {
                _isPainting = false;
                FinalizePainting();
                return true;
            }
            return false;
        }

        private void ApplyBiome(Vector3 center) {
            if (_selectedBiome == null) return;

            // Apply textures
            ApplyBiomeTextures(center, _selectedBiome);

            // Place objects if enabled
            if (AutoPlaceObjects) {
                PlaceBiomeObjects(center, BrushRadius, _selectedBiome);
            }
        }

        private void ApplyBiomeTextures(Vector3 center, BiomeDefinition biome) {
            var affected = PaintCommand.GetAffectedVertices(center, BrushRadius, Context);
            var landblockDataCache = new Dictionary<ushort, TerrainEntry[]>();

            // Collect texture changes
            var batchChanges = new Dictionary<ushort, Dictionary<byte, uint>>();

            foreach (var (lbId, vIndex, pos) in affected) {
                if (!landblockDataCache.TryGetValue(lbId, out var data)) {
                    data = Context.TerrainSystem.GetLandblockTerrain(lbId);
                    if (data == null) continue;
                    landblockDataCache[lbId] = data;
                }

                // Skip if already modified in this stroke to prevent flickering/re-randomization
                if (_pendingTextureChanges.TryGetValue(lbId, out var existingList) && existingList.Any(c => c.VertexIndex == vIndex)) {
                    continue;
                }

                byte originalType = data[vIndex].Type;

                // Choose primary or secondary texture randomly
                byte newType = _random.NextDouble() < biome.SecondaryMix
                    ? (byte)biome.SecondaryTexture
                    : (byte)biome.PrimaryTexture;

                if (originalType == newType) continue;

                // Record change
                if (!_pendingTextureChanges.TryGetValue(lbId, out var list)) {
                    list = new List<(int, byte, byte)>();
                    _pendingTextureChanges[lbId] = list;
                }
                list.Add((vIndex, originalType, newType));

                // Prepare batch update for immediate preview
                if (!batchChanges.TryGetValue(lbId, out var lbChanges)) {
                    lbChanges = new Dictionary<byte, uint>();
                    batchChanges[lbId] = lbChanges;
                }
                var newEntry = data[vIndex] with { Type = newType };
                lbChanges[(byte)vIndex] = newEntry.ToUInt();
            }

            // Apply batch update
            if (batchChanges.Count > 0) {
                var modified = Context.TerrainSystem.UpdateLandblocksBatch(TerrainField.Type, batchChanges);
                Context.MarkLandblocksModified(modified);
            }
        }

        private void PlaceBiomeObjects(Vector3 center, float radius, BiomeDefinition biome) {
            // Calculate area of the brush
            // We want density to mean "objects per unit area".
            // User snippet says "Objects per 24x24 cell".
            // Area of brush circle = PI * r^2.
            // Area of 24x24 cell = 576.
            // Number of cells covered = Area / 576.
            // Objects per cell = Density.
            // Total objects to attempt = (Area / 576) * Density * Multiplier.

            // However, doing this every frame while dragging will spawn TONS of objects.
            // We need to limit spawn rate based on distance moved or just randomness?
            // If we just spawn randomly in the circle, we will over-density areas we hover over.
            // A common technique is to only spawn if we moved enough distance, or check density at location.
            // But checking density is hard.

            // Simpler approach for "Paint":
            // Only try to spawn a few objects per frame based on probability.
            // OR: grid-based tracking.

            // Let's try probability per frame based on brush size.
            // Or just spawn a fraction of the "full density" amount, assuming the user is moving.

            float area = MathF.PI * radius * radius;
            float cellArea = 24f * 24f;
            float numCells = area / cellArea;

            foreach (var objDef in biome.Objects) {
                float expectedObjects = numCells * objDef.Density * ObjectDensityMultiplier;

                // Scale down because we run this every frame (approx 60fps or mouse move rate).
                // If we assume user drags, we only want to fill NEW area.
                // A simple hack is to multiply by a small factor, e.g. 0.05.
                int numToSpawn = 0;
                double prob = expectedObjects * 0.02;

                if (prob < 1.0) {
                    if (_random.NextDouble() < prob) numToSpawn = 1;
                }
                else {
                    numToSpawn = (int)prob;
                }

                for (int i = 0; i < numToSpawn; i++) {
                    // Random position within circle
                    float angle = (float)_random.NextDouble() * MathF.PI * 2f;
                    float distance = MathF.Sqrt((float)_random.NextDouble()) * radius; // Sqrt for uniform distribution

                    float x = center.X + MathF.Cos(angle) * distance;
                    float y = center.Y + MathF.Sin(angle) * distance;

                    // Check slope
                    float slope = CalculateSlopeAtPosition(x, y);
                    if (slope < objDef.MinSlope || slope > objDef.MaxSlope)
                        continue;

                    float z = Context.GetHeightAtPosition(x, y);

                    // Random scale and rotation
                    float scaleT = (float)_random.NextDouble();
                    float scale = objDef.MinScale + (objDef.MaxScale - objDef.MinScale) * scaleT;
                    var rotation = Quaternion.CreateFromAxisAngle(
                        Vector3.UnitZ, (float)_random.NextDouble() * MathF.PI * 2f);

                    var obj = new StaticObject {
                        Id = objDef.ObjectId,
                        Origin = new Vector3(x, y, z),
                        Orientation = rotation,
                        Scale = new Vector3(scale)
                    };

                    PlaceObject(obj);
                }
            }
        }

        private void PlaceObject(StaticObject obj) {
            // Determine landblock
            int lbX = (int)(obj.Origin.X / 192f);
            int lbY = (int)(obj.Origin.Y / 192f);
            if (lbX < 0 || lbX >= 255 || lbY < 0 || lbY >= 255) return;

            ushort lbKey = (ushort)((lbX << 8) | lbY);

            var docId = $"landblock_{lbKey:X4}";
            var doc = Context.TerrainSystem.DocumentManager.GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();

            if (doc != null) {
                int index = doc.AddStaticObject(obj);

                // Eagerly load render data
                Context.TerrainSystem.Scene._objectManager.GetRenderData(obj.Id, obj.IsSetup);

                _pendingObjects.Add((lbKey, obj));
                _pendingObjectIndices.Add((lbKey, index));
                Context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            }
        }

        private float CalculateSlopeAtPosition(float x, float y) {
            // Simple slope calculation using neighbors
            float h = Context.GetHeightAtPosition(x, y);
            float hx = Context.GetHeightAtPosition(x + 1f, y);
            float hy = Context.GetHeightAtPosition(x, y + 1f);

            Vector3 v1 = new Vector3(1f, 0, hx - h);
            Vector3 v2 = new Vector3(0, 1f, hy - h);
            Vector3 normal = Vector3.Normalize(Vector3.Cross(v1, v2));

            // Angle between normal and up vector
            float dot = Vector3.Dot(normal, Vector3.UnitZ);
            float angleRad = MathF.Acos(dot);
            return angleRad * (180f / MathF.PI);
        }

        private void FinalizePainting() {
            if (_pendingTextureChanges.Count == 0 && _pendingObjects.Count == 0) return;

            // Create combined command
            // Note: Objects are already added to the document!
            // So we must pass the INDICES to the command so it knows they are pre-applied.
            // AND we must ensure that Redo (Execute) doesn't duplicate them.

            var command = new BiomePaintCommand(
                Context,
                _selectedBiome?.Name ?? "Custom",
                new Dictionary<ushort, List<(int, byte, byte)>>(_pendingTextureChanges),
                new List<(ushort, StaticObject)>(_pendingObjects),
                new List<(ushort, int)>(_pendingObjectIndices));

            _commandHistory.ExecuteCommand(command);

            _pendingTextureChanges.Clear();
            _pendingObjects.Clear();
            _pendingObjectIndices.Clear();
        }

        public void AddAssetToBiome(uint objectId) {
            if (_selectedBiome == null) return;

            // Check if already exists
            if (_selectedBiome.Objects.Any(o => o.ObjectId == objectId)) return;

            _selectedBiome.Objects.Add(new BiomeObject {
                ObjectId = objectId,
                Density = 0.1f // Default density
            });

            // Since BiomeDefinition.Objects is an ObservableCollection,
            // the UI will update automatically.
        }
    }
}
