using Unity.Entities;
using UnityEngine;

public class HoveredBlockAuthoring : MonoBehaviour
{
    public class Baker : Baker<HoveredBlockAuthoring>
    {
        public override void Bake(HoveredBlockAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new HoveredBlock
            {
                Chunk = Entity.Null
            });
        }
    }
}
