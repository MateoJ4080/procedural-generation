using Unity.Entities;
using Unity.Mathematics;

public struct HoveredBlock : IComponentData
{
    public Entity Chunk;
    public int3 BlockPosition;
}