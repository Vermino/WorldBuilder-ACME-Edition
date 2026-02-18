using DatReaderWriter.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorldBuilder.Shared.Models {
    public class BiomeDefinition {
        public string Name { get; set; } = "";
        public TerrainTextureType PrimaryTexture { get; set; }
        public TerrainTextureType SecondaryTexture { get; set; } // For variation
        public float SecondaryMix { get; set; } = 0.2f; // 20% secondary

        public ObservableCollection<BiomeObject> Objects { get; set; } = new();
    }

    public class BiomeObject {
        public uint ObjectId { get; set; }
        public float Density { get; set; } // Objects per 24x24 cell
        public float MinScale { get; set; } = 1.0f;
        public float MaxScale { get; set; } = 1.0f;
        public float MinSlope { get; set; } = 0f;
        public float MaxSlope { get; set; } = 45f;
    }
}
