using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class PlayerTrackerBridge : MonoBehaviour
{
    [SerializeField] private Transform player;

    private EntityManager entityManager;
    private Entity trackerEntity;

    void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void Start()
    {
        var query = entityManager.CreateEntityQuery(typeof(PlayerTracker));
        trackerEntity = query.GetSingletonEntity();
    }

    void Update()
    {
        if (player == null || !entityManager.Exists(trackerEntity))
            return;

        var tracker = entityManager.GetComponentData<PlayerTracker>(trackerEntity);
        tracker.exists = true;
        tracker.playerPosition = (float3)player.position;

        entityManager.SetComponentData(trackerEntity, tracker);
    }
}