using UnityEngine;
using System.Threading.Tasks;

namespace ReadyPlayerMe.Samples
{
    [RequireComponent(typeof(ThirdPersonMovement), typeof(PlayerInput))]
    public class ThirdPersonController : MonoBehaviour
    {
        private const float FALL_TIMEOUT = 0.15f;

        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int JumpHash = Animator.StringToHash("JumpTrigger");
        private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int EnterCustomAnimHash = Animator.StringToHash("EnterCustomAnim");
        private static readonly int ExitCustomAnimHash = Animator.StringToHash("ExitCustomAnim");

        private Transform playerCamera;
        private Animator animator;
        private Vector2 inputVector;
        private Vector3 moveVector;
        private GameObject avatar;
        private ThirdPersonMovement thirdPersonMovement;
        private PlayerInput playerInput;

        private float fallTimeoutDelta;

        [SerializeField]
        [Tooltip("Useful to toggle input detection in editor")]
        private bool inputEnabled = true;
        private bool isInitialized;

        // 用于替换动画片段
        private bool _isCustomAnimPlaying = false;
        private AnimatorOverrideController overrideController;
        [Header("Animation")]
        [Tooltip("Assign the placeholder clip used in the 'CustomAnim' state in your Animator Controller.")]
        public AnimationClip placeholderAnim;

        private void Init()
        {
            thirdPersonMovement = GetComponent<ThirdPersonMovement>();
            playerInput = GetComponent<PlayerInput>();
            playerInput.OnJumpPress += OnJump;
            isInitialized = true;
        }

        public void Setup(GameObject target, RuntimeAnimatorController runtimeAnimatorController)
        {
            if (!isInitialized)
            {
                Init();
            }

            avatar = target;
            thirdPersonMovement.Setup(avatar);
            animator = avatar.GetComponent<Animator>();
            //animator.runtimeAnimatorController = runtimeAnimatorController;
            overrideController = new AnimatorOverrideController(runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;


            animator.applyRootMotion = false;

        }

        private void Update()
        {
            if (avatar == null)
            {
                return;
            }
            if (inputEnabled)
            {
                playerInput.CheckInput();
                var xAxisInput = playerInput.AxisHorizontal;
                var yAxisInput = playerInput.AxisVertical;
                thirdPersonMovement.Move(xAxisInput, yAxisInput);
                thirdPersonMovement.SetIsRunning(playerInput.IsHoldingLeftShift);
            }
            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            var isGrounded = thirdPersonMovement.IsGrounded();
            animator.SetFloat(MoveSpeedHash, thirdPersonMovement.CurrentMoveSpeed);
            animator.SetBool(IsGroundedHash, isGrounded);
            if (isGrounded)
            {
                fallTimeoutDelta = FALL_TIMEOUT;
                animator.SetBool(FreeFallHash, false);
            }
            else
            {
                if (fallTimeoutDelta >= 0.0f)
                {
                    fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    animator.SetBool(FreeFallHash, true);
                }
            }
        }

        private void OnJump()
        {
            if (thirdPersonMovement.TryJump())
            {
                animator.SetTrigger(JumpHash);
            }
        }

        public void StartCustomAnimationSequence(AnimationClip customClip)
        {
            _ = PlayCustomAnimAndWait(customClip);
        }

        // 传入一个 AnimationClip，替换 CustomAnim 的 state motion，播放完后恢复原clip
        private async Task PlayCustomAnimAndWait(AnimationClip customClip)
        {
            if (_isCustomAnimPlaying)
            {
                Debug.LogWarning("Custom animation already playing.");
                return;
            }

            if (placeholderAnim == null)
            {
                Debug.LogError("PlaceholderAnim is not assigned in the ThirdPersonController inspector.");
                return;
            }

            _isCustomAnimPlaying = true;

            const string stateToWaitFor = "CustomAnim";
            const int layerIndex = 0;

            try
            {
                // 1. Override the animation
                overrideController[placeholderAnim.name] = customClip;

                // 2. Trigger the state transition
                animator.SetTrigger(EnterCustomAnimHash);

                // 3. Wait until the animator has entered the target state
                await Task.Yield(); // Wait one frame for the transition to begin
                while (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateToWaitFor))
                {
                    await Task.Yield();
                }

                // 4. Wait for the animation to complete
                while (animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime < 1.0f)
                {
                    // Ensure we are still in the same state, in case an interrupt occurs
                    if (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateToWaitFor)) break;
                    await Task.Yield();
                }

                // 5. Trigger the exit transition from the state
                animator.SetTrigger(ExitCustomAnimHash);
            }
            finally
            {
                // 6. Restore the original animation and reset the flag
                // This 'finally' block ensures restoration even if the task is cancelled or an error occurs.
                overrideController[placeholderAnim.name] = placeholderAnim;
                _isCustomAnimPlaying = false;
            }
        }
    }
}
