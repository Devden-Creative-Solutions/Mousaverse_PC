//Copyright Filmstorm (C) 2018 - Movement Controller for Root Motion and built in IK solver
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This script requires you to have setup your animator with 3 parameters, "InputMagnitude", "InputX", "InputZ"
//With a blend tree to control the inputmagnitude and allow blending between animations.
[RequireComponent(typeof(CharacterController))]
public class MovementInput : MonoBehaviour
{
    #region Variables
    private float InputX; //Left and Right Inputs
    private float InputZ; //Forward and Back Inputs
    private Vector3 desiredMoveDirection; //Vector that holds desired Move Direction
    private bool blockRotationPlayer = false; //Block the rotation of the player?
    [Range(0, 0.5f)] public float desiredRotationSpeed = 0.1f; //Speed of the players rotation
    public Animator anim; //Animator
    private float Speed; //Speed player is moving
    [Range(0, 1f)] public float allowPlayerRotation = 0.1f; //Allow player rotation from inputs once past x
    public Camera cam; //Main camera (make sure tag is MainCamera)
    public CharacterController controller; //Character Controller, auto added on script addition
    private bool isGrounded; //is Grounded - work in progress

    private Vector3 rightFootPosition, leftFootPosition, leftFootIkPosition, rightFootIkPosition;
    private Quaternion leftFootIkRotation, rightFootIkRotation;
    private float lastPelvisPositionY, lastRightFootPositionY, lastLeftFootPositionY;

    [Header("Feet Grounder")]
    public bool enableFeetIk = true;
    [Range(0, 2)] [SerializeField] private float heightFromGroundRaycast = 1.14f;
    [Range(0, 2)] [SerializeField] private float raycastDownDistance = 1.5f;
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private float pelvisOffset = 0f;
    [Range(0, 1)] [SerializeField] private float pelvisUpAndDownSpeed = 0.28f;
    [Range(0, 1)] [SerializeField] private float feetToIkPositionSpeed = 0.5f;

    public string leftFootAnimVariableName = "LeftFootCurve";
    public string rightFootAnimVariableName = "RightFootCurve";

    public bool useProIkFeature = false;
    public bool showSolverDebug = true;



    [Header("Animation Smoothing")]
    [Range(0, 1f)]
    public float HorizontalAnimSmoothTime = 0.2f; //InputX dampening
    [Range(0, 1f)]
    public float VerticalAnimTime = 0.2f; //InputZ dampening
    [Range(0, 1f)]
    public float StartAnimTime = 0.3f; //dampens the time of starting the player after input is pressed
    [Range(0, 1f)]
    public float StopAnimTime = 0.15f; //dampens the time of stopping the player after release of input


    private float verticalVel; //Vertical velocity -- currently work in progress
    private Vector3 moveVector; //movement vector -- currently work in progress

    #endregion

    #region Initialization
    // Initialization of variables
    void Start()
    {
        anim = this.GetComponent<Animator>();
        cam = Camera.main;
        controller = this.GetComponent<CharacterController>();

        if (anim == null)
            Debug.LogError("We require " + transform.name + " game object to have an animator. This will allow for Foot IK to function");
    }


    // Update is called once per frame
    void Update()
    {
        InputMagnitude();

    }

    private void OnDestroy()
    {
        Debug.Log("Destroyed user");
    }

    #endregion

    #region PlayerMovement

    void PlayerMoveAndRotation()
    {
        InputX = Input.GetAxis("Horizontal");
        InputZ = Input.GetAxis("Vertical");

        var camera = Camera.main;
        var forward = cam.transform.forward;
        var right = cam.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        desiredMoveDirection = forward * InputZ + right * InputX;

        if (blockRotationPlayer == false)
        {
            //transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), desiredRotationSpeed);
        }
    }

    void InputMagnitude()
    {
        //Calculate Input Vectors
        InputX = Input.GetAxis("Horizontal");
        InputZ = Input.GetAxis("Vertical");

        //anim.SetFloat("InputZ", InputZ, VerticalAnimTime, Time.deltaTime * 2f);
        //anim.SetFloat("InputX", InputX, HorizontalAnimSmoothTime, Time.deltaTime * 2f);

        //Calculate the Input Magnitude
        Speed = new Vector2(InputX, InputZ).sqrMagnitude;

        //Physically move player
        if (Speed > allowPlayerRotation)
        {
            //anim.SetFloat("InputMagnitude", Speed, StartAnimTime, Time.deltaTime);
            //PlayerMoveAndRotation();
        }
        else if (Speed < allowPlayerRotation)
        {
            //anim.SetFloat("InputMagnitude", Speed, StopAnimTime, Time.deltaTime);
        }
    }

    #endregion

    #region FeetGrounding

    /// <summary>
    /// We are updating the AdjustFeetTarget method and also find the position of each foot inside our Solver Position.
    /// </summary>
    private void FixedUpdate()
    {
        if (enableFeetIk == false) { return; }
        if (anim == null) { return; }

        AdjustFeetTarget(ref rightFootPosition, HumanBodyBones.RightFoot);
        AdjustFeetTarget(ref leftFootPosition, HumanBodyBones.LeftFoot);

        //find and raycast to the ground to find positions
        FeetPositionSolver(rightFootPosition, ref rightFootIkPosition, ref rightFootIkRotation); // handle the solver for right foot
        FeetPositionSolver(leftFootPosition, ref leftFootIkPosition, ref leftFootIkRotation); //handle the solver for the left foot

    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (enableFeetIk == false) { return; }
        if (anim == null) { return; }

        MovePelvisHeight();

        //right foot ik position and rotation -- utilise the pro features in here
        anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);


        if (useProIkFeature)
        {
            anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, anim.GetFloat(rightFootAnimVariableName));
        }
        else
        {
            //anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);
        }

        MoveFeetToIkPoint(AvatarIKGoal.RightFoot, rightFootIkPosition, rightFootIkRotation, ref lastRightFootPositionY);

        //left foot ik position and rotation -- utilise the pro features in here
        anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);

        if (useProIkFeature)
        {
            anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, anim.GetFloat(leftFootAnimVariableName));
        }
        else
        {
            //anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);
        }

        MoveFeetToIkPoint(AvatarIKGoal.LeftFoot, leftFootIkPosition, leftFootIkRotation, ref lastLeftFootPositionY);
    }

    #endregion

    #region FeetGroundingMethods

    /// <summary>
    /// Moves the feet to ik point.
    /// </summary>
    /// <param name="foot">Foot.</param>
    /// <param name="positionIkHolder">Position ik holder.</param>
    /// <param name="rotationIkHolder">Rotation ik holder.</param>
    /// <param name="lastFootPositionY">Last foot position y.</param>
    void MoveFeetToIkPoint(AvatarIKGoal foot, Vector3 positionIkHolder, Quaternion rotationIkHolder, ref float lastFootPositionY)
    {
        Vector3 targetIkPosition = anim.GetIKPosition(foot);

        if (positionIkHolder != Vector3.zero)
        {
            targetIkPosition = transform.InverseTransformPoint(targetIkPosition);
            positionIkHolder = transform.InverseTransformPoint(positionIkHolder);

            float yVariable = Mathf.Lerp(lastFootPositionY, positionIkHolder.y, feetToIkPositionSpeed);
            targetIkPosition.y += yVariable;

            lastFootPositionY = yVariable;

            targetIkPosition = transform.TransformPoint(targetIkPosition);

            anim.SetIKRotation(foot, rotationIkHolder);
        }

        anim.SetIKPosition(foot, targetIkPosition);
    }
    /// <summary>
    /// Moves the height of the pelvis.
    /// </summary>
    private void MovePelvisHeight()
    {

        if (rightFootIkPosition == Vector3.zero || leftFootIkPosition == Vector3.zero || lastPelvisPositionY == 0)
        {
            lastPelvisPositionY = anim.bodyPosition.y;
            return;
        }

        float lOffsetPosition = leftFootIkPosition.y - transform.position.y;
        float rOffsetPosition = rightFootIkPosition.y - transform.position.y;

        float totalOffset = (lOffsetPosition < rOffsetPosition) ? lOffsetPosition : rOffsetPosition;

        Vector3 newPelvisPosition = anim.bodyPosition + Vector3.up * totalOffset;

        newPelvisPosition.y = Mathf.Lerp(lastPelvisPositionY, newPelvisPosition.y, pelvisUpAndDownSpeed);

        anim.bodyPosition = newPelvisPosition;

        lastPelvisPositionY = anim.bodyPosition.y;

    }

    /// <summary>
    /// We are locating the Feet position via a Raycast and then Solving
    /// </summary>
    /// <param name="fromSkyPosition">From sky position.</param>
    /// <param name="feetIkPositions">Feet ik positions.</param>
    /// <param name="feetIkRotations">Feet ik rotations.</param>
    private void FeetPositionSolver(Vector3 fromSkyPosition, ref Vector3 feetIkPositions, ref Quaternion feetIkRotations)
    {
        //raycast handling section 
        RaycastHit feetOutHit;

        if (showSolverDebug)
            Debug.DrawLine(fromSkyPosition, fromSkyPosition + Vector3.down * (raycastDownDistance + heightFromGroundRaycast), Color.yellow);

        if (Physics.Raycast(fromSkyPosition, Vector3.down, out feetOutHit, raycastDownDistance + heightFromGroundRaycast, environmentLayer))
        {
            //finding our feet ik positions from the sky position
            feetIkPositions = fromSkyPosition;
            feetIkPositions.y = feetOutHit.point.y + pelvisOffset;
            feetIkRotations = Quaternion.FromToRotation(Vector3.up, feetOutHit.normal) * transform.rotation;

            return;
        }

        feetIkPositions = Vector3.zero; //it didn't work :(

    }
    /// <summary>
    /// Adjusts the feet target.
    /// </summary>
    /// <param name="feetPositions">Feet positions.</param>
    /// <param name="foot">Foot.</param>
    private void AdjustFeetTarget(ref Vector3 feetPositions, HumanBodyBones foot)
    {
        feetPositions = anim.GetBoneTransform(foot).position;
        feetPositions.y = transform.position.y + heightFromGroundRaycast;

    }

    #endregion

}





