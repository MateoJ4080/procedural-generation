using TMPro;
using Unity.Entities;
using UnityEngine;

public class FlyModeText : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private EntityManager entityManager;
    private EntityQuery playerQuery;

    private void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FirstPersonPlayer>());
    }

    private void Update()
    {
        if (playerQuery.IsEmpty) return;

        Entity player = playerQuery.GetSingletonEntity();
        Entity character = entityManager
            .GetComponentData<FirstPersonPlayer>(player)
            .ControlledCharacter;

        bool flyMode = entityManager
            .GetComponentData<FirstPersonCharacterComponent>(character)
            .FlyMode;

        text.text = flyMode ? "(Fly Mode: On)" : "(Fly Mode: Off)";
    }
}