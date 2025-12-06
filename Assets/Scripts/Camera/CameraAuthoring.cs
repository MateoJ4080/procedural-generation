using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CameraAuthoring : MonoBehaviour
{
    public class Baker : Baker<CameraAuthoring>
    {
        public override void Bake(CameraAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var parent = GetEntity(authoring.transform.parent, TransformUsageFlags.Dynamic);

            AddComponent(entity, new Parent { Value = parent });
            AddComponent<PostTransformMatrix>(entity);
        }
    }
}
