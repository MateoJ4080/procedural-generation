using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class GameManagerAuthoring : MonoBehaviour
{
    public class Baker : Baker<GameManagerAuthoring>
    {
        public override void Bake(GameManagerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlayerTracker
            {
                exists = false,
                playerPosition = float3.zero
            });
        }
    }
}