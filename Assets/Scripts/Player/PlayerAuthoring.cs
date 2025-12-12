using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public float speed = 5f;

    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<PlayerTag>(entity);
            AddComponent<LocalTransform>(entity);
            AddComponent<PlayerMoveInput>(entity);
            AddComponent<CameraLookInput>(entity);
            AddComponent<CameraSettings>(entity);
            AddComponent(entity, new PlayerSpeed { Value = authoring.speed });
        }
    }
}
