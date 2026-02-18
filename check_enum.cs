using DatReaderWriter.Enums;
using System;

class Program {
    static void Main() {
        foreach (var name in Enum.GetNames(typeof(TerrainTextureType))) {
            Console.WriteLine(name);
        }
    }
}
