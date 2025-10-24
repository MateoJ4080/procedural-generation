using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    [SerializeField] private float speed;

    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerTag());

            AddComponent(entity, new LocalTransform
            {
                Position = authoring.transform.position,
                Rotation = authoring.transform.rotation,
                Scale = 1f
            });
        }
    }
}
