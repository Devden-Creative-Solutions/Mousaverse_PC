﻿using UnityEngine;

public abstract class CharacterStateBase : ICharacterState
{
    public static readonly ICharacterState GROUNDED_STATE = new GroundedCharacterState();
    public static readonly ICharacterState JUMPING_STATE = new JumpingCharacterState();
    public static readonly ICharacterState IN_AIR_STATE = new InAirCharacterState();

    public virtual void OnEnter(Character character) { }

    public virtual void OnExit(Character character) { }

    public virtual void Update(Character character)
    {
        character.ApplyGravity();
        if (!UIElements.Instance.canUseJoystick)
        {
            character.MoveVector = PlayerInput.GetMovementInput(character.Camera);
            if (Input.GetMouseButton(1) || Input.GetKey(KeyCode.M))
                character.ControlRotation = PlayerInput.GetMouseRotationInput(PlayerInput._horizontalAxisCam, PlayerInput._verticalAxisCam);
        }
        else
        {
            character.MoveVector = PlayerInput.GetMovementInputJoystick(character.Camera);
            character.ControlRotation = PlayerInput.GetMouseRotationInputJoystick();
        }

        //if (Input.GetMouseButton(1))
        //    character.ControlRotation = PlayerInput.GetMouseRotationInput();
    }

    public virtual void ToState(Character character, ICharacterState state)
    {
        character.CurrentState.OnExit(character);
        character.CurrentState = state;
        character.CurrentState.OnEnter(character);
    }
}
