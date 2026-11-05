using UnityEngine;
using UnityEngine.InputSystem;

namespace ChiikawaField
{
    /// <summary>WASD/stick movement, smooth facing, and a grounded Space/button jump.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ChiikawaPlayerController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform visual;
        [SerializeField] private Transform viewCamera;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Animator animator;

        [Header("Movement (Unity units per second)")]
        [SerializeField, Min(0f)] private float speed = 20f;
        [SerializeField, Min(0f)] private float turnSpeed = 10f;
        [SerializeField, Min(0f)] private float jumpVelocity = 7f;
        [SerializeField, Min(0.1f)] private float gravity = 14.715f;

        private CharacterController controller;
        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction jumpAction;
        private float verticalVelocity;
        private static readonly int IsRunning = Animator.StringToHash("IsRunning");

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (inputActions == null)
            {
                Debug.LogError("Assign the input actions asset to Chiikawa's controller.", this);
                enabled = false;
                return;
            }

            // A private copy avoids changing the shared asset or another player's controls.
            runtimeActions = Instantiate(inputActions);
            var playerMap = runtimeActions.FindActionMap("Player", true);
            moveAction = playerMap.FindAction("Move", true);
            jumpAction = playerMap.FindAction("Jump", true);
            playerMap.Enable();
        }

        private void OnDisable()
        {
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                Destroy(runtimeActions);
            }
            moveAction = null;
            jumpAction = null;
            verticalVelocity = 0f;
            if (animator != null)
                animator.SetBool(IsRunning, false);
        }

        private void Update()
        {
            if (moveAction == null || jumpAction == null)
                return;

            Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            Transform cameraTransform = viewCamera != null ? viewCamera : transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            Vector3 direction = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // Small downward motion keeps contact with the ground.

            if (controller.isGrounded && jumpAction.WasPressedThisFrame())
                verticalVelocity = jumpVelocity;

            verticalVelocity -= gravity * Time.deltaTime;
            controller.Move((direction * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

            // Drive the in-place run from grounded movement; collisions and jumps stay physical.
            if (animator != null)
            {
                Vector3 velocity = controller.velocity;
                velocity.y = 0f;
                animator.SetBool(IsRunning, controller.isGrounded &&
                    direction.sqrMagnitude > 0.001f && velocity.sqrMagnitude > 0.01f);
            }

            if (visual != null && direction.sqrMagnitude > 0.001f)
            {
                Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
                visual.rotation = Quaternion.Slerp(visual.rotation, facing,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
        }
    }
}
