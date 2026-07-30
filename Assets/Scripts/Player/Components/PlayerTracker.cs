using Unity.Entities;
using Unity.Mathematics;

public struct PlayerTracker : IComponentData
{
    public bool exists;
    public float3 playerPosition;
}
