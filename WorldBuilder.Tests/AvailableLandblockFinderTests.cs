using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using DatReaderWriter.Lib.IO;
using DatReaderWriter.Types;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorldBuilder.Lib;
using WorldBuilder.Shared.Lib;
using Xunit;

namespace WorldBuilder.Tests {
    public class AvailableLandblockFinderTests {
        private readonly Mock<IDatReaderWriter> _datsMock;
        private readonly Mock<ILogger<AvailableLandblockFinder>> _loggerMock;
        private readonly AvailableLandblockFinder _finder;

        public AvailableLandblockFinderTests() {
            _datsMock = new Mock<IDatReaderWriter>();
            _loggerMock = new Mock<ILogger<AvailableLandblockFinder>>();
            _finder = new AvailableLandblockFinder(_datsMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task FindAvailableLandblocksAsync_ShouldReturnIds_WhenLandBlockInfoIsMissing() {
            // Setup: Only 0x0001 has LandBlockInfo (occupied), 0x0002 is missing (available)
            // Note: Parallel scanning in Finder might be non-deterministic if we don't mock ALL calls.
            // But we can't mock 65536 calls.
            // However, the finder scans sequentially now (per my fix).
            // But mock behavior for "Everything else"?
            // Moq default returns default(T), so TryGet returns false (out null).
            // This means essentially ALL landblocks are available by default in the mock.

            // Let's explicitly make 0x0001 OCCUPIED.
            uint lbId = 0x0001;
            uint infoId = (lbId << 16) | 0xFFFE;
            var occupiedLbi = new LandBlockInfo {
                NumCells = 10,
                Objects = new List<Stab>(),
                Buildings = new List<BuildingInfo>()
            };

            _datsMock.Setup(d => d.TryGet(infoId, out occupiedLbi)).Returns(true);

            // Run
            var results = await _finder.FindAvailableLandblocksAsync();

            // 0x0001 should NOT be in results
            Assert.DoesNotContain((ushort)0x0001, results);

            // 0x0002 should be in results (default mock returns false for TryGet -> available)
            Assert.Contains((ushort)0x0002, results);
        }

        [Fact]
        public async Task FindAvailableLandblocksAsync_ShouldReturnIds_WhenLandBlockInfoIsEmpty() {
            // 0x0003 has LBI but 0 objs, 0 bldgs, 0 cells
            uint lbId = 0x0003;
            uint infoId = (lbId << 16) | 0xFFFE;
            var emptyLbi = new LandBlockInfo {
                NumCells = 0,
                Objects = new List<Stab>(),
                Buildings = new List<BuildingInfo>()
            };

            _datsMock.Setup(d => d.TryGet(infoId, out emptyLbi)).Returns(true);

            var results = await _finder.FindAvailableLandblocksAsync();

            Assert.Contains((ushort)0x0003, results);
        }

        [Fact]
        public async Task FindAvailableLandblocksAsync_ShouldExclude_WhenHasObjects() {
            uint lbId = 0x0004;
            uint infoId = (lbId << 16) | 0xFFFE;
            var lbi = new LandBlockInfo {
                NumCells = 0,
                Objects = new List<Stab> { new Stab() },
                Buildings = new List<BuildingInfo>()
            };

            _datsMock.Setup(d => d.TryGet(infoId, out lbi)).Returns(true);

            var results = await _finder.FindAvailableLandblocksAsync();

            Assert.DoesNotContain((ushort)0x0004, results);
        }

        [Fact]
        public async Task FindAvailableLandblocksAsync_ShouldExclude_WhenHasBuildings() {
            uint lbId = 0x0005;
            uint infoId = (lbId << 16) | 0xFFFE;
            var lbi = new LandBlockInfo {
                NumCells = 0,
                Objects = new List<Stab>(),
                Buildings = new List<BuildingInfo> { new BuildingInfo() }
            };

            _datsMock.Setup(d => d.TryGet(infoId, out lbi)).Returns(true);

            var results = await _finder.FindAvailableLandblocksAsync();

            Assert.DoesNotContain((ushort)0x0005, results);
        }

        [Fact]
        public async Task FindAvailableLandblocksAsync_ShouldExclude_WhenHasCells() {
            uint lbId = 0x0006;
            uint infoId = (lbId << 16) | 0xFFFE;
            var lbi = new LandBlockInfo {
                NumCells = 1, // Dungeon cells present
                Objects = new List<Stab>(),
                Buildings = new List<BuildingInfo>()
            };

            _datsMock.Setup(d => d.TryGet(infoId, out lbi)).Returns(true);

            var results = await _finder.FindAvailableLandblocksAsync();

            Assert.DoesNotContain((ushort)0x0006, results);
        }
    }
}
