using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.CharacterController;

[Serializable]
public struct FirstPersonCharacterComponent : IComponentData
{
    public float GroundMaxSpeed;
    public float GroundedMovementSharpness;
    public float AirAcceleration;
    public float AirMaxSpeed;
    public float AirDrag;
    public float JumpSpeed;
    public float3 Gravity;
    public bool PreventAirAccelerationAgainstUngroundedHits;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

    public float DetectionRange;
    public float LookSensitivity;

    public float MinViewAngle;
    public float MaxViewAngle;

    public Entity ViewEntity;
    public float ViewPitchDegrees;
    public quaternion ViewLocalRotation;

    public bool FlyMode;
    public float HorizontalFlySpeed;
    public float VerticalFlySpeed;
}

[Serializable]
public struct FirstPersonCharacterControl : IComponentData
{
    public float3 MoveVector;
    public float2 LookDegreesDelta;
    public bool Jump;

    public bool FlyUp;
    public bool FlyDown;
}

[Serializable]
public struct FirstPersonCharacterView : IComponentData
{
    public Entity CharacterEntity;
}
