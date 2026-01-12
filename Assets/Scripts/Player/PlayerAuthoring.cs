using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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

            AddComponent<LocalTransform>(entity);
            AddComponent<PlayerTag>(entity);
            AddComponent<PlayerMoveInput>(entity);
            AddComponent<CameraLookInput>(entity);
            AddComponent<CameraSettings>(entity);
            AddComponent<PhysicsVelocity>(entity);
            AddComponent<PhysicsGravityFactor>(entity);
            AddComponent<PhysicsWorldIndex>(entity);
            AddComponent(entity, PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 1f));
            AddComponent(entity, new PlayerSpeed { Value = authoring.speed });

            var box = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(1, 2, 1),
                Orientation = quaternion.identity,
                BevelRadius = 0
            });

            AddComponent(entity, new PhysicsCollider { Value = box });

        }
    }
}
