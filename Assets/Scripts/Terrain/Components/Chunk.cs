using Unity.Entities;
using Unity.Mathematics;

public struct Chunk : IComponentData
{
    public int3 Position;
    public int Width;
    public int Height;
    public int Depth;
}