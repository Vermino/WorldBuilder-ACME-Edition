using Chorizite.Core.Render;
using Chorizite.OpenGLSDLBackend;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Types;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Numerics;
using WorldBuilder.Editors.Landscape;
using WorldBuilder.Lib;
using WorldBuilder.Shared.Lib;
using Xunit;

namespace WorldBuilder.Tests {
    public class EnvCellManagerTests {
        private readonly Mock<OpenGLRenderer> _rendererMock;
        private readonly Mock<IDatReaderWriter> _datsMock;
        private readonly Mock<IShader> _shaderMock;
        private readonly Mock<TextureDiskCache> _textureCacheMock;
        private readonly EnvCellManager _manager;

        public EnvCellManagerTests() {
            _rendererMock = new Mock<OpenGLRenderer>(); // This might be hard to mock if it's not an interface or doesn't have virtuals
            _datsMock = new Mock<IDatReaderWriter>();
            _shaderMock = new Mock<IShader>();
            // EnvCellManager accepts null cache, so we can pass null or mock
            _manager = new EnvCellManager(null!, _datsMock.Object, _shaderMock.Object, null);
        }

        [Fact]
        public void LinkPortals_ShouldConnectBothCells() {
            var cellA = new EnvCell { Id = 0x12340100 };
            cellA.CellPortals.Add(new CellPortal { OtherCellId = 0, OtherPortalId = 0 });

            var cellB = new EnvCell { Id = 0x12340101 };
            cellB.CellPortals.Add(new CellPortal { OtherCellId = 0, OtherPortalId = 0 }); // Index 0
            cellB.CellPortals.Add(new CellPortal { OtherCellId = 0, OtherPortalId = 0 }); // Index 1

            _manager.LinkPortals(cellA, 0, cellB, 1);

            Assert.Equal(0x0101, cellA.CellPortals[0].OtherCellId);
            Assert.Equal(1, cellA.CellPortals[0].OtherPortalId);

            Assert.Equal(0x0100, cellB.CellPortals[1].OtherCellId);
            Assert.Equal(0, cellB.CellPortals[1].OtherPortalId);
        }

        [Fact]
        public void UnlinkPortal_ShouldClearConnection() {
            var cell = new EnvCell { Id = 0x12340100 };
            cell.CellPortals.Add(new CellPortal { OtherCellId = 0x0101, OtherPortalId = 5 });

            _manager.UnlinkPortal(cell, 0);

            Assert.Equal(0, cell.CellPortals[0].OtherCellId);
            Assert.Equal(0, cell.CellPortals[0].OtherPortalId);
        }
    }
}
