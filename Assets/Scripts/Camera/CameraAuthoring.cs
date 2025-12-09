using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CameraAuthoring : MonoBehaviour
{
    public float Sensitivity = 2f;
    public float PitchMin = -89f;
    public float PitchMax = 89f;

    public class Baker : Baker<CameraAuthoring>
    {
        public override void Bake(CameraAuthoring authoring)
        {
            var camEntity = GetEntity(TransformUsageFlags.Dynamic);
            var parent = GetEntity(authoring.transform.parent, TransformUsageFlags.Dynamic);

            AddComponent(camEntity, new Parent { Value = parent });
            AddComponent<CameraTag>(camEntity);
            AddComponent<CameraLookInput>(camEntity);
            AddComponent(camEntity, new CameraConfig
            {
                Sensitivity = authoring.Sensitivity,
                PitchMin = authoring.PitchMin,
                PitchMax = authoring.PitchMax
            });
        }
    }
}
