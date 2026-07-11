using Unity.Entities;
using UnityEngine;

public class HighlightTagAuthoring : MonoBehaviour
{
    public class Baker : Baker<HighlightTagAuthoring>
    {
        public override void Bake(HighlightTagAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent<HighlightTag>(entity);
        }
    }
}
