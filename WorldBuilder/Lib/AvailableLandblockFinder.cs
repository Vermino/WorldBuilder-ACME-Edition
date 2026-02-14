using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Options;
using Microsoft.Extensions.Logging;
using WorldBuilder.Shared.Lib;

namespace WorldBuilder.Lib {
    public class AvailableLandblockFinder {
        private readonly IDatReaderWriter _dats;
        private readonly ILogger<AvailableLandblockFinder> _logger;
        private readonly ConcurrentBag<ushort> _availableLandblocks = new();
        private bool _isScanning;

        public AvailableLandblockFinder(IDatReaderWriter dats, ILogger<AvailableLandblockFinder> logger) {
            _dats = dats;
            _logger = logger;
        }

        public async Task<List<ushort>> FindAvailableLandblocksAsync(IProgress<int>? progress = null) {
            if (_isScanning) {
                return _availableLandblocks.ToList();
            }

            _isScanning = true;
            // Clear previous results
            while (_availableLandblocks.TryTake(out _)) { }

            _logger.LogInformation("Starting landblock availability scan...");

            // Scan range: 0x0000 to 0xFFFF landblocks
            int total = 65536;
            int processed = 0;

            await Task.Run(() => {
                for (int i = 0; i < total; i++) {
                    ushort lbId = (ushort)i;
                    // LandBlockInfo ID is (lbId << 16) | 0xFFFE
                    uint infoId = ((uint)lbId << 16) | 0xFFFE;

                    bool available = false;
                    if (!_dats.TryGet<LandBlockInfo>(infoId, out var lbi)) {
                        // No LandBlockInfo -> empty/ocean -> available
                        available = true;
                    }
                    else {
                        // Check if it has any static objects or buildings
                        if (lbi.Objects.Count == 0 && lbi.Buildings.Count == 0) {
                            available = true;
                        }
                    }

                    if (available) {
                        _availableLandblocks.Add(lbId);
                    }

                    processed++;
                    if (processed % 1000 == 0) {
                        progress?.Report((int)((double)processed / total * 100));
                    }
                }
            });

            _isScanning = false;
            _logger.LogInformation("Scan complete. Found {Count} available landblocks.", _availableLandblocks.Count);

            return _availableLandblocks.OrderBy(x => x).ToList();
        }
    }
}
