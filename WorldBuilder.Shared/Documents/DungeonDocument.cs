using CommunityToolkit.Mvvm.DependencyInjection;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using DatReaderWriter.Options;
using DatReaderWriter.Types;
using MemoryPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WorldBuilder.Shared.Lib;

namespace WorldBuilder.Shared.Documents {
    [MemoryPackable]
    public partial class DungeonMetadata {
        public string Name { get; set; } = "New Dungeon";
        public int MinLevel { get; set; } = 1;
        public int MaxLevel { get; set; } = 10;
        public string Theme { get; set; } = "Default";
    }

    [MemoryPackable]
    public partial class DungeonData {
        public DungeonMetadata Metadata { get; set; } = new();
        public List<ushort> ManagedCellIds { get; set; } = new();
    }

    public partial class DungeonDocument : BaseDocument {
        public override string Type => nameof(DungeonDocument);

        [MemoryPackInclude]
        private DungeonData _data = new();

        // In-memory cache of EnvCells
        private readonly Dictionary<ushort, EnvCell> _envCells = new();

        // In-memory cache of buildings
        private readonly List<BuildingInfo> _buildings = new();

        public IReadOnlyDictionary<ushort, EnvCell> EnvCells => _envCells;
        public IReadOnlyList<BuildingInfo> Buildings => _buildings;

        public DungeonDocument(ILogger logger) : base(logger) {
        }

        public DungeonMetadata Metadata => _data.Metadata;
        public IReadOnlyList<ushort> ManagedCellIds => _data.ManagedCellIds;

        protected override async Task<bool> InitInternal(IDatReaderWriter datreader, DocumentManager documentManager) {
            var lbIdHex = Id.Replace("dungeon_", "").Replace("landblock_", "");
            if (uint.TryParse(lbIdHex, System.Globalization.NumberStyles.HexNumber, null, out var lbId)) {
                var infoId = (lbId << 16) | 0xFFFE;

                if (datreader.TryGet<LandBlockInfo>(infoId, out var lbi)) {
                // Load existing buildings
                _buildings.Clear();
                _buildings.AddRange(lbi.Buildings);

                    // If we have managed IDs (from projection), load them
                    foreach (var cellId in _data.ManagedCellIds) {
                        if (!_envCells.ContainsKey(cellId)) {
                            uint fullId = (lbId << 16) | cellId;
                            if (datreader.TryGet<EnvCell>(fullId, out var cell)) {
                                _envCells[cellId] = cell;
                            }
                            else {
                                _logger.LogWarning("[DungeonDoc] Managed cell 0x{CellId:X4} not found in DATs", cellId);
                            }
                        }
                    }
                }
            }

            ClearDirty();
            return true;
        }

        protected override byte[] SaveToProjectionInternal() {
            return MemoryPackSerializer.Serialize(_data);
        }

        protected override bool LoadFromProjectionInternal(byte[] projection) {
            _data = MemoryPackSerializer.Deserialize<DungeonData>(projection) ?? new();
            return true;
        }

        protected override Task<bool> SaveToDatsInternal(IDatReaderWriter datwriter, int iteration = 0) {
            var lbIdHex = Id.Replace("dungeon_", "").Replace("landblock_", "");
            var lbId = uint.Parse(lbIdHex, System.Globalization.NumberStyles.HexNumber);
            var infoId = (lbId << 16) | 0xFFFE;

            _logger.LogInformation("[DungeonDoc] Saving dungeon 0x{LbId:X4} — {CellCount} cells", lbId, _data.ManagedCellIds.Count);

            if (!datwriter.TryGet<LandBlockInfo>(infoId, out var lbi)) {
                lbi = new LandBlockInfo { Id = infoId };
            }

            foreach (var cellId in _data.ManagedCellIds) {
                if (_envCells.TryGetValue(cellId, out var cell)) {
                    if (!datwriter.TrySave(cell, iteration)) {
                         _logger.LogError("[DungeonDoc] Failed to save EnvCell 0x{CellId:X4}", cellId);
                         return Task.FromResult(false);
                    }
                }
            }

            // Save buildings
            lbi.Buildings.Clear();
            lbi.Buildings.AddRange(_buildings);

            // Update LandBlockInfo NumCells
            lbi.NumCells = (uint)_data.ManagedCellIds.Count;

            if (!datwriter.TrySave(lbi, iteration)) {
                _logger.LogError("[DungeonDoc] Failed to save LandBlockInfo 0x{InfoId:X8}", infoId);
                return Task.FromResult(false);
            }

            _logger.LogInformation("[DungeonDoc] Saved LandBlockInfo and {Count} cells.", _data.ManagedCellIds.Count);
            return Task.FromResult(true);
        }

        public void AddCell(EnvCell cell) {
            ushort cellId = (ushort)(cell.Id & 0xFFFF);
            if (!_data.ManagedCellIds.Contains(cellId)) {
                _data.ManagedCellIds.Add(cellId);
            }
            _envCells[cellId] = cell;
            MarkDirty();
        }

        public void RemoveCell(ushort cellId) {
            if (_data.ManagedCellIds.Contains(cellId)) {
                _data.ManagedCellIds.Remove(cellId);
                _envCells.Remove(cellId);
                MarkDirty();
            }
        }

        public EnvCell? GetCell(ushort cellId) {
            return _envCells.TryGetValue(cellId, out var cell) ? cell : null;
        }

        public void InstantiateBlueprint(uint modelId, Vector3 position, Quaternion orientation, IDatReaderWriter dats, ILogger logger) {
            var blueprint = BuildingBlueprintCache.GetBlueprint(modelId, dats, logger);
            if (blueprint == null) return;

            var lbIdHex = Id.Replace("dungeon_", "").Replace("landblock_", "");
            var lbId = uint.Parse(lbIdHex, System.Globalization.NumberStyles.HexNumber);

            // Calculate safe cell count to avoid ID collision
            ushort maxId = 0;
            if (_data.ManagedCellIds.Count > 0) {
                 maxId = _data.ManagedCellIds.Max();
            }
            // Start allocation after max used ID (assuming 0x0100 base)
            uint safeNumCells = (maxId >= 0x0100) ? (uint)(maxId - 0x0100 + 1) : 0;

            var result = BuildingBlueprintCache.InstantiateBlueprint(blueprint, position, orientation, lbId, safeNumCells, dats, 0, logger);

            if (result != null) {
                _buildings.Add(result.Value.building);

                // Track newly created cells
                // BuildingBlueprintCache allocates sequentially starting at safeNumCells + 0x0100
                ushort startId = (ushort)(safeNumCells + 0x0100);
                for (int i = 0; i < result.Value.cellCount; i++) {
                     ushort newId = (ushort)(startId + i);
                     if (!_data.ManagedCellIds.Contains(newId)) {
                         _data.ManagedCellIds.Add(newId);
                     }
                     // Load into memory (blueprint cache saved to DAT, so we can load it)
                     uint fullId = (lbId << 16) | newId;
                     if (dats.TryGet<EnvCell>(fullId, out var newCell)) {
                         _envCells[newId] = newCell;
                     }
                }
                MarkDirty();
            }
        }

        public bool Apply(BaseDocumentEvent evt) {
            // Handle updates from editor if needed (e.g. cell modified)
            // For now, EnvCellManager might modify EnvCell objects directly?
            // If so, we just need to ensure we mark dirty if they change.
            return true;
        }
    }
}
