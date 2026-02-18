using DatReaderWriter.Enums;
using System.Collections.Generic;
using WorldBuilder.Shared.Models;

namespace WorldBuilder.Lib {
    public static class BiomeLibrary {
        public static BiomeDefinition Forest => new() {
            Name = "Forest",
            PrimaryTexture = TerrainTextureType.Grass,
            SecondaryTexture = TerrainTextureType.GrassDark,
            SecondaryMix = 0.3f,
            Objects = new() {
                new() { ObjectId = 0x010001A7, Density = 0.3f, MinScale = 0.8f, MaxScale = 1.2f }, // Oak tree
                new() { ObjectId = 0x010001A8, Density = 0.2f, MinScale = 0.6f, MaxScale = 1.0f }, // Small tree
                new() { ObjectId = 0x010001B2, Density = 0.1f }, // Bush
                new() { ObjectId = 0x010001C3, Density = 0.05f } // Rock
            }
        };

        public static BiomeDefinition Desert => new() {
            Name = "Desert",
            PrimaryTexture = TerrainTextureType.Sand,
            SecondaryTexture = TerrainTextureType.SandDark,
            SecondaryMix = 0.15f,
            Objects = new() {
                new() { ObjectId = 0x01000245, Density = 0.05f }, // Cactus
                new() { ObjectId = 0x01000246, Density = 0.08f, MinScale = 0.5f, MaxScale = 1.5f }, // Rock
                new() { ObjectId = 0x01000247, Density = 0.02f } // Dead tree
            }
        };

        public static BiomeDefinition Mountain => new() {
            Name = "Mountain",
            PrimaryTexture = TerrainTextureType.Rock,
            SecondaryTexture = TerrainTextureType.Rock2,
            SecondaryMix = 0.4f,
            Objects = new() {
                new() { ObjectId = 0x01000246, Density = 0.1f, MinScale = 0.8f, MaxScale = 2.0f }, // Large Rock
                new() { ObjectId = 0x010001C3, Density = 0.2f, MinScale = 0.5f, MaxScale = 1.0f }  // Small Rock
            }
        };

        public static BiomeDefinition Swamp => new() {
            Name = "Swamp",
            PrimaryTexture = TerrainTextureType.Mud,
            SecondaryTexture = TerrainTextureType.Water,
            SecondaryMix = 0.1f,
            Objects = new() {
                new() { ObjectId = 0x01000247, Density = 0.15f, MinScale = 0.8f, MaxScale = 1.2f } // Dead tree
            }
        };

        public static BiomeDefinition Snow => new() {
            Name = "Snow",
            PrimaryTexture = TerrainTextureType.Snow,
            SecondaryTexture = TerrainTextureType.Ice,
            SecondaryMix = 0.2f,
            Objects = new() {
                new() { ObjectId = 0x010001A7, Density = 0.2f, MinScale = 0.8f, MaxScale = 1.5f } // Pine tree (using Oak ID as placeholder if needed, but assuming appropriate IDs)
            }
        };
    }
}
