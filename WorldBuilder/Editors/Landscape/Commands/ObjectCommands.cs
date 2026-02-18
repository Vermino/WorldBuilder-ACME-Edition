using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WorldBuilder.Lib.History;
using WorldBuilder.Shared.Documents;

namespace WorldBuilder.Editors.Landscape.Commands {

    /// <summary>
    /// Base class for object manipulation commands.
    /// </summary>
    public abstract class ObjectCommand : ICommand {
        protected readonly TerrainEditingContext _context;
        protected readonly ushort _landblockKey;
        protected readonly int _objectIndex;

        public abstract string Description { get; }
        public bool CanExecute => true;
        public bool CanUndo => true;
        public List<string> AffectedDocumentIds => new() { $"landblock_{_landblockKey:X4}" };

        protected ObjectCommand(TerrainEditingContext context, ushort landblockKey, int objectIndex) {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _landblockKey = landblockKey;
            _objectIndex = objectIndex;
        }

        protected LandblockDocument? GetDocument() {
            var docId = $"landblock_{_landblockKey:X4}";
            return _context.TerrainSystem.DocumentManager
                .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
        }

        protected void InvalidateScene() {
            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
        }

        public abstract bool Execute();
        public abstract bool Undo();
    }

    /// <summary>
    /// Command to move a static object to a new position.
    /// </summary>
    public class MoveObjectCommand : ObjectCommand {
        private readonly Vector3 _oldPosition;
        private readonly Vector3 _newPosition;

        public override string Description => "Move object";

        public MoveObjectCommand(TerrainEditingContext context, ushort landblockKey, int objectIndex,
            Vector3 oldPosition, Vector3 newPosition) : base(context, landblockKey, objectIndex) {
            _oldPosition = oldPosition;
            _newPosition = newPosition;
        }

        public override bool Execute() {
            var doc = GetDocument();
            if (doc == null || _objectIndex >= doc.StaticObjectCount) return false;

            var obj = doc.GetStaticObject(_objectIndex);
            doc.UpdateStaticObject(_objectIndex, new StaticObject {
                Id = obj.Id, IsSetup = obj.IsSetup, Origin = _newPosition,
                Orientation = obj.Orientation, Scale = obj.Scale
            });
            InvalidateScene();
            return true;
        }

        public override bool Undo() {
            var doc = GetDocument();
            if (doc == null || _objectIndex >= doc.StaticObjectCount) return false;

            var obj = doc.GetStaticObject(_objectIndex);
            doc.UpdateStaticObject(_objectIndex, new StaticObject {
                Id = obj.Id, IsSetup = obj.IsSetup, Origin = _oldPosition,
                Orientation = obj.Orientation, Scale = obj.Scale
            });
            InvalidateScene();
            return true;
        }
    }

    /// <summary>
    /// Command to rotate a static object.
    /// </summary>
    public class RotateObjectCommand : ObjectCommand {
        private readonly Quaternion _oldOrientation;
        private readonly Quaternion _newOrientation;

        public override string Description => "Rotate object";

        public RotateObjectCommand(TerrainEditingContext context, ushort landblockKey, int objectIndex,
            Quaternion oldOrientation, Quaternion newOrientation) : base(context, landblockKey, objectIndex) {
            _oldOrientation = oldOrientation;
            _newOrientation = newOrientation;
        }

        public override bool Execute() {
            var doc = GetDocument();
            if (doc == null || _objectIndex >= doc.StaticObjectCount) return false;

            var obj = doc.GetStaticObject(_objectIndex);
            doc.UpdateStaticObject(_objectIndex, new StaticObject {
                Id = obj.Id, IsSetup = obj.IsSetup, Origin = obj.Origin,
                Orientation = _newOrientation, Scale = obj.Scale
            });
            InvalidateScene();
            return true;
        }

        public override bool Undo() {
            var doc = GetDocument();
            if (doc == null || _objectIndex >= doc.StaticObjectCount) return false;

            var obj = doc.GetStaticObject(_objectIndex);
            doc.UpdateStaticObject(_objectIndex, new StaticObject {
                Id = obj.Id, IsSetup = obj.IsSetup, Origin = obj.Origin,
                Orientation = _oldOrientation, Scale = obj.Scale
            });
            InvalidateScene();
            return true;
        }
    }

    /// <summary>
    /// Command to add a new static object to a landblock.
    /// </summary>
    public class AddObjectCommand : ICommand {
        private readonly TerrainEditingContext _context;
        private readonly ushort _landblockKey;
        private readonly StaticObject _object;
        private int _addedIndex = -1;

        public string Description => "Add object";
        public bool CanExecute => true;
        public bool CanUndo => true;
        public List<string> AffectedDocumentIds => new() { $"landblock_{_landblockKey:X4}" };

        public AddObjectCommand(TerrainEditingContext context, ushort landblockKey, StaticObject obj) {
            _context = context;
            _landblockKey = landblockKey;
            _object = obj;
        }

        /// <summary>
        /// Gets the index of the added object (valid after Execute).
        /// </summary>
        public int AddedIndex => _addedIndex;

        public bool Execute() {
            var docId = $"landblock_{_landblockKey:X4}";
            var doc = _context.TerrainSystem.DocumentManager
                .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
            if (doc == null) return false;

            _addedIndex = doc.AddStaticObject(_object);

            // Eagerly load render data so the object renders immediately
            _context.TerrainSystem.Scene._objectManager.GetRenderData(_object.Id, _object.IsSetup);

            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            return true;
        }

        public bool Undo() {
            var docId = $"landblock_{_landblockKey:X4}";
            var doc = _context.TerrainSystem.DocumentManager
                .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
            if (doc == null) return false;

            doc.RemoveStaticObject(_addedIndex);
            _context.ObjectSelection.Deselect();
            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            return true;
        }
    }

    /// <summary>
    /// Command to remove a static object from a landblock.
    /// </summary>
    public class RemoveObjectCommand : ICommand {
        private readonly TerrainEditingContext _context;
        private readonly ushort _landblockKey;
        private readonly int _objectIndex;
        private StaticObject _removedObject;

        public string Description => "Remove object";
        public bool CanExecute => true;
        public bool CanUndo => true;
        public List<string> AffectedDocumentIds => new() { $"landblock_{_landblockKey:X4}" };

        public RemoveObjectCommand(TerrainEditingContext context, ushort landblockKey, int objectIndex) {
            _context = context;
            _landblockKey = landblockKey;
            _objectIndex = objectIndex;
        }

        public bool Execute() {
            var docId = $"landblock_{_landblockKey:X4}";
            var doc = _context.TerrainSystem.DocumentManager
                .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
            if (doc == null || _objectIndex >= doc.StaticObjectCount) return false;

            _removedObject = doc.GetStaticObject(_objectIndex);
            doc.RemoveStaticObject(_objectIndex);
            _context.ObjectSelection.Deselect();
            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            return true;
        }

        public bool Undo() {
            var docId = $"landblock_{_landblockKey:X4}";
            var doc = _context.TerrainSystem.DocumentManager
                .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
            if (doc == null) return false;

            // Re-insert at original index if possible
            if (_objectIndex <= doc.StaticObjectCount) {
                doc.AddStaticObject(_removedObject);
            }
            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            return true;
        }
    }

    /// <summary>
    /// Command to add multiple static objects to various landblocks.
    /// </summary>
    public class BatchAddObjectCommand : ICommand {
        private readonly TerrainEditingContext _context;
        private readonly List<(ushort LandblockKey, StaticObject Object)> _objects;
        private readonly List<(ushort LandblockKey, int AddedIndex)> _addedIndices = new();
        private bool _alreadyApplied;

        public string Description => $"Add {_objects.Count} objects";
        public bool CanExecute => true;
        public bool CanUndo => true;
        public List<string> AffectedDocumentIds {
            get {
                var ids = new HashSet<string>();
                foreach (var (key, _) in _objects) {
                    ids.Add($"landblock_{key:X4}");
                }
                return new List<string>(ids);
            }
        }

        public BatchAddObjectCommand(
            TerrainEditingContext context,
            List<(ushort LandblockKey, StaticObject Object)> objects,
            List<(ushort LandblockKey, int AddedIndex)>? preAppliedIndices = null) {
            _context = context;
            _objects = objects;

            if (preAppliedIndices != null) {
                _addedIndices.AddRange(preAppliedIndices);
                _alreadyApplied = true;
            }
        }

        public bool Execute() {
            if (_alreadyApplied) {
                _alreadyApplied = false;
                return true;
            }

            _addedIndices.Clear();
            var docCache = new Dictionary<ushort, LandblockDocument>();

            foreach (var (key, obj) in _objects) {
                if (!docCache.TryGetValue(key, out var doc)) {
                    var docId = $"landblock_{key:X4}";
                    doc = _context.TerrainSystem.DocumentManager
                        .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
                    if (doc != null) docCache[key] = doc;
                }

                if (doc != null) {
                    int index = doc.AddStaticObject(obj);
                    _addedIndices.Add((key, index));

                    // Eagerly load render data so the object renders immediately
                    _context.TerrainSystem.Scene._objectManager.GetRenderData(obj.Id, obj.IsSetup);
                }
            }

            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            return true;
        }

        public bool Undo() {
            var docCache = new Dictionary<ushort, LandblockDocument>();

            // Remove in reverse order of addition to preserve indices
            for (int i = _addedIndices.Count - 1; i >= 0; i--) {
                var (key, index) = _addedIndices[i];
                if (!docCache.TryGetValue(key, out var doc)) {
                    var docId = $"landblock_{key:X4}";
                    doc = _context.TerrainSystem.DocumentManager
                        .GetOrCreateDocumentAsync<LandblockDocument>(docId).GetAwaiter().GetResult();
                    if (doc != null) docCache[key] = doc;
                }

                if (doc != null) {
                    doc.RemoveStaticObject(index);
                }
            }

            _context.ObjectSelection.Deselect();
            _context.TerrainSystem.Scene.InvalidateStaticObjectsCache();
            return true;
        }
    }
}
