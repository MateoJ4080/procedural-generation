using Unity.Entities;

public struct TerrainConfig : IComponentData
{
    // Information about these parameters in the docs folder
    public float Frequency;
    public float Amplitude;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
}