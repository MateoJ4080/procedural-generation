using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using System.Collections.Generic;
using Unity.Profiling;

public partial class ChunkColliderSystem : SystemBase
{
    private static readonly ProfilerMarker CreateColliderMarker = new("ChunkMeshApply.CreateCollider");

    private Queue<PendingMesh> _colliderQueue = new();

    protected override void OnUpdate()
    {
        // Create only one collider per frame to prevent frame spikes
        if (_colliderQueue.Count > 0)
        {
            var pending = _colliderQueue.Dequeue();

            if (EntityManager.Exists(pending.Entity))
            {
                using (CreateColliderMarker.Auto())
                {
                    var trianglesInt3 = new NativeArray<int3>(pending.Triangles.Length / 3, Allocator.Temp);

                    for (int i = 0; i < trianglesInt3.Length; i++)
                    {
                        trianglesInt3[i] = new int3(
                            pending.Triangles[i * 3],
                            pending.Triangles[i * 3 + 1],
                            pending.Triangles[i * 3 + 2]);
                    }

                    var collider = Unity.Physics.MeshCollider.Create(
                        pending.Vertices.AsArray(),
                        trianglesInt3
                    );

                    trianglesInt3.Dispose();

                    var physicsCollider = new PhysicsCollider
                    {
                        Value = collider
                    };

                    if (EntityManager.HasComponent<PhysicsCollider>(pending.Entity))
                        EntityManager.SetComponentData(pending.Entity, physicsCollider);
                    else
                        EntityManager.AddComponentData(pending.Entity, physicsCollider);

                    pending.Dispose();

                    var transform = SystemAPI.GetComponent<LocalTransform>(pending.Entity);
                    Debug.Log($"Collider generated for {transform.Position}");
                }
            }
        }
    }

    public void Enqueue(PendingMesh pending)
    {
        // Delay collider creation to avoid creating multiple expensive colliders in the same frame
        _colliderQueue.Enqueue(pending);
    }
}