using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

[UpdateAfter(typeof(ChunkMeshSystem))]
public partial struct ChunkColliderSystem : ISystem
{
    private EntityCommandBuffer ecb;

    public void OnUpdate(ref SystemState state)
    {
        var ecbSystem = state.World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        ecb = ecbSystem.CreateCommandBuffer();

        foreach (var (chunk, entity) in SystemAPI.Query<RefRO<Chunk>>().WithNone<PhysicsCollider>().WithEntityAccess())
        {
            var tag = SystemAPI.GetSingletonRW<ChunkMeshData>();
            tag.ValueRW.currentHandle.Complete();

            var meshData = SystemAPI.GetSingleton<ChunkMeshData>();

            var flatTris = meshData.Triangles;
            var trianglesInt3 = new NativeList<int3>(Allocator.Temp); ;

            for (int i = 0; i < meshData.Triangles.Length; i += 3)
            {
                trianglesInt3.Add(new int3(flatTris[i], flatTris[i + 1], flatTris[i + 2]));
            }

            var meshCollider = MeshCollider.Create(meshData.Vertices.AsArray(), trianglesInt3.AsArray());
            var physCollider = new PhysicsCollider { Value = meshCollider };

            ecb.AddComponent(entity, physCollider);
            ecb.AddSharedComponent(entity, new PhysicsWorldIndex());

            UnityEngine.Debug.Log($"Collider valid = {physCollider.IsValid}");
        }
    }
}
