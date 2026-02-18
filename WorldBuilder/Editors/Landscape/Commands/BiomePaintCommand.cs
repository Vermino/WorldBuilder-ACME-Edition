using System.Collections.Generic;
using System.Linq;
using WorldBuilder.Shared.Documents;
using DatReaderWriter.Enums;
using WorldBuilder.Lib.History;

namespace WorldBuilder.Editors.Landscape.Commands {
    public class BiomePaintCommand : ICommand {
        private readonly TerrainEditingContext _context;
        private readonly PaintCommand _paintCommand;
        private readonly BatchAddObjectCommand _addObjectCommand;
        private readonly string _biomeName;

        public string Description => $"Biome Paint: {_biomeName}";
        public bool CanExecute => true;
        public bool CanUndo => true;

        public List<string> AffectedDocumentIds {
            get {
                var ids = new HashSet<string>(_paintCommand.AffectedDocumentIds);
                ids.UnionWith(_addObjectCommand.AffectedDocumentIds);
                return new List<string>(ids);
            }
        }

        public BiomePaintCommand(
            TerrainEditingContext context,
            string biomeName,
            Dictionary<ushort, List<(int VertexIndex, byte OriginalType, byte NewType)>> textureChanges,
            List<(ushort LandblockKey, StaticObject Object)> newObjects,
            List<(ushort LandblockKey, int AddedIndex)>? preAppliedIndices = null) {

            _context = context;
            _biomeName = biomeName;

            // We pass a dummy type (Grassland) because the changes dictionary contains the actual values.
            // The inner command's description is ignored/wrapped by this command.
            _paintCommand = new PaintCommand(context, TerrainTextureType.Grassland, textureChanges);

            _addObjectCommand = new BatchAddObjectCommand(context, newObjects, preAppliedIndices);
        }

        public bool Execute() {
            bool result = true;
            // Execute paint first, then objects
            result &= _paintCommand.Execute();
            result &= _addObjectCommand.Execute();
            return result;
        }

        public bool Undo() {
            bool result = true;
            // Undo in reverse order: remove objects, then revert paint
            result &= _addObjectCommand.Undo();
            result &= _paintCommand.Undo();
            return result;
        }
    }
}
