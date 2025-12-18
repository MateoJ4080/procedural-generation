// using System.Diagnostics;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Jobs;
// using Unity.Mathematics;
// using Unity.Physics;
// using UnityEditor.Build.Pipeline.Tasks;

// [UpdateAfter(typeof(ChunkMeshSystem))]
// public partial struct ChunkColliderSystem : ISystem
// {
//     private EntityCommandBuffer ecb;

//     public void OnUpdate(ref SystemState state)
//     {
//         var swTotal = Stopwatch.StartNew();

//         var ecbSystem = state.World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
//         ecb = ecbSystem.CreateCommandBuffer();

//         foreach (var (chunk, entity) in SystemAPI.Query<RefRO<Chunk>>().WithNone<PhysicsCollider>().WithEntityAccess())
//         {
//             var swForeach = Stopwatch.StartNew();
//             UnityEngine.Debug.Log("<color=red>[DEBUG]</color> Starting foreach...");

//             var swMeshWait = Stopwatch.StartNew();
//             var tag = SystemAPI.GetSingletonRW<ChunkMeshData>();
//             tag.ValueRW.currentMeshHandle.Complete();
//             swMeshWait.Stop();

//             var meshData = SystemAPI.GetSingleton<ChunkMeshData>();
//             var flatTris = meshData.Triangles;

//             var swTriangles = Stopwatch.StartNew();
//             var trianglesInt3 = new NativeList<int3>(Allocator.Temp);
//             for (int i = 0; i < flatTris.Length; i += 3)
//             {
//                 trianglesInt3.Add(new int3(flatTris[i], flatTris[i + 1], flatTris[i + 2]));
//             }
//             swTriangles.Stop();

//             var swCollider = Stopwatch.StartNew();
//             var meshCollider = MeshCollider.Create(meshData.Vertices.AsArray(), trianglesInt3.AsArray());
//             var physCollider = new PhysicsCollider { Value = meshCollider };
//             swCollider.Stop();

//             ecb.AddComponent(entity, physCollider);
//             ecb.AddSharedComponent(entity, new PhysicsWorldIndex());

//             swForeach.Stop();

//             UnityEngine.Debug.Log($"<color=yellow>[DEBUG]</color> Mesh wait: {swMeshWait.Elapsed.TotalMilliseconds:F2} ms");
//             UnityEngine.Debug.Log($"<color=lime>[DEBUG]</color> Triangles build: {swTriangles.Elapsed.TotalMilliseconds:F2} ms");
//             UnityEngine.Debug.Log($"<color=green>[DEBUG]</color> Collider create: {swCollider.Elapsed.TotalMilliseconds:F2} ms");
//             UnityEngine.Debug.Log($"<color=cyan>[DEBUG]</color> Foreach total: {swForeach.Elapsed.TotalMilliseconds:F2} ms");
//         }

//         swTotal.Stop();
//         UnityEngine.Debug.Log($"<color=white>[DEBUG]</color> System total: {swTotal.Elapsed.TotalMilliseconds:F2} ms");
//     }
// }
