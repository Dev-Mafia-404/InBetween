using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Cinemachine;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FPS_Controller : MonoBehaviour
    {
        // ============================================================================
        // MOVEMENT SETTINGS
        // ============================================================================

        [Header("Movement")]
        [Tooltip("Walk speed of the character in m/s")]
        public float WalkSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Tooltip("Key to sprint (hold)")]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

        // ============================================================================
        // JUMP SETTINGS
        // ============================================================================

        [Header("Jump")]
        [Tooltip("Force applied to player when jumping")]
        public float JumpForce = 5.0f;

        [Tooltip("Cooldown before being able to jump again")]
        public float JumpCooldown = 0.50f;

        [Tooltip("Key to jump")]
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;

        // ============================================================================
        // CROUCH SETTINGS
        // ============================================================================

        [Header("Crouch")]
        [Tooltip("If enabled, crouch feature is available")]
        [SerializeField] private bool enableCrouch = true;

        [Tooltip("Key to toggle crouch")]
        [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;

        [Tooltip("Walk speed while crouching")]
        [SerializeField] private float crouchWalkSpeed = 1.0f;

        [Tooltip("Camera Y offset when crouching")]
        [SerializeField] private float crouchCameraOffset = -0.5f;

        [Tooltip("Default camera Y offset when standing")]
        [SerializeField] private float defaultCameraHeightOffset = 0.0f;

        [Tooltip("Speed of camera offset lerp when crouching")]
        [SerializeField] private float crouchLerpSpeed = 5.0f;

        [Tooltip("Character controller height when standing")]
        [SerializeField] private float standingHeight = 2.0f;

        [Tooltip("Character controller height when crouching")]
        [SerializeField] private float crouchingHeight = 1.0f;

    

        // ============================================================================
        // AUDIO SETTINGS
        // ============================================================================

        [Header("Audio")]
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;
        [SerializeField] private AudioClip[] crouchAudioClips;
        [Range(0, 1)][SerializeField] private float crouchAudioVolume = 0.5f;

        // ============================================================================
        // GROUND DETECTION SETTINGS
        // ============================================================================

        [Header("Ground Detection")]
        [Tooltip("If the character is grounded or not")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        // ============================================================================
        // CAMERA SETTINGS
        // ============================================================================

        [Header("Cinemachine Camera")]
        [Tooltip("Direct reference to the First Person Cinemachine Virtual Camera")]
        [SerializeField] private CinemachineCamera firstPersonCamera;

        [Tooltip("The camera target transform for head position")]
        public GameObject CinemachineCameraTarget;

        // ============================================================================
        // FIRST PERSON LOOK SETTINGS
        // ============================================================================

        [Header("First Person Look")]
        [Tooltip("Maximum pitch angle when looking up")]
        [SerializeField] private float pitchMax = 80.0f;

        [Tooltip("Minimum pitch angle when looking down")]
        [SerializeField] private float pitchMin = -80.0f;

        [Tooltip("If enabled, yaw rotates the player body")]
        [SerializeField] private bool rotateBodyYaw = true;

        // ============================================================================
        // PLAYER BODY VISIBILITY SETTINGS
        // ============================================================================

        [Header("Body Visibility")]
        [Tooltip("If enabled, show the player body mesh in first person")]
        [SerializeField] private bool showPlayerMesh = false;

        [Tooltip("Layer(s) that represent the player body")]
        [SerializeField] private LayerMask playerBodyLayers = 0;

        // ============================================================================
        // ZOOM SETTINGS
        // ============================================================================

        [Header("Zoom")]
        [Tooltip("If enabled, zoom feature is available")]
        [SerializeField] private bool enableZoom = true;

        [Tooltip("Vertical field of view while zoomed")]
        [SerializeField] private float zoomFOV = 30.0f;

        [Tooltip("Default vertical field of view")]
        [SerializeField] private float normalFOV = 60.0f;

        [Tooltip("Speed of FOV transition when zooming")]
        [SerializeField] private float zoomSmoothSpeed = 5.0f;

        // ============================================================================
        // SPRINT FOV SETTINGS
        // ============================================================================

        [Header("Sprint FOV")]
        [Tooltip("If enabled, FOV changes when sprinting")]
        [SerializeField] private bool enableSprintFOV = true;

        [Tooltip("Vertical field of view while sprinting")]
        [SerializeField] private float sprintFOV = 70.0f;

        [Tooltip("Speed of FOV transition when sprinting")]
        [SerializeField] private float sprintFOVSmoothSpeed = 3.0f;

        // ============================================================================
        // PRIVATE FIELDS - CAMERA
        // ============================================================================

        private float _yaw;
        private float _pitch;
        private bool _isZooming = false;
        private bool _isCrouching = false;
        private float _currentVerticalFOV;
        private float _targetVerticalFOV;
        private Vector3 _cameraTargetLocalPos;
        private float _currentCameraYOffset;

        // ============================================================================
        // PRIVATE FIELDS - MOVEMENT
        // ============================================================================

        private float _speed;
        private float _animationBlend;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private float _jumpCooldownDelta;

        // ============================================================================
        // PRIVATE FIELDS - ANIMATION
        // ============================================================================

        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDMotionSpeed;

        // ============================================================================
        // PRIVATE FIELDS - REFERENCES
        // ============================================================================

        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private Camera _mainCameraComponent;
        private Animator _animator;
        private bool _cachedMainCameraMask;
        private int _mainCameraBaseMask;
        private float _normalControllerHeight;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        // ============================================================================
        // CONSTANTS
        // ============================================================================

        private const float _threshold = 0.01f;
        private bool _hasAnimator;

        // ============================================================================
        // PROPERTIES
        // ============================================================================

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        // ============================================================================
        // INITIALIZATION
        // ============================================================================

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        private void Start()
        {
            CacheMainCameraMask();

            // --- Initialize rotation from camera target ---
            if (CinemachineCameraTarget != null)
            {
                var euler = CinemachineCameraTarget.transform.rotation.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x;
                if (_pitch > 180f) _pitch -= 360f;
                _pitch = ClampAngle(_pitch, pitchMin, pitchMax);
                _cameraTargetLocalPos = CinemachineCameraTarget.transform.localPosition;
                _currentCameraYOffset = defaultCameraHeightOffset;
            }
            else
            {
                _yaw = transform.eulerAngles.y;
                _pitch = 0f;
                _cameraTargetLocalPos = Vector3.zero;
                _currentCameraYOffset = defaultCameraHeightOffset;
            }

            // --- Cache components ---
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _normalControllerHeight = _controller.height;
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();
            _jumpCooldownDelta = JumpCooldown;

            // --- Get main camera component ---
            _mainCameraComponent = GetMainCameraComponent();

            // --- Initialize Cinemachine camera FOV ---
            if (firstPersonCamera != null)
            {
                _currentVerticalFOV = firstPersonCamera.Lens.FieldOfView;
                _targetVerticalFOV = _currentVerticalFOV;
                normalFOV = _currentVerticalFOV;
            }

            // --- Apply initial mesh culling ---
            ApplyMainCameraCulling(!showPlayerMesh);
        }

        // ============================================================================
        // UPDATE LOOP
        // ============================================================================

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            GroundedCheck();
            HandleSprintInput();
            HandleJumpInput();
            HandleCrouchInput();
            ApplyJumpAndGravity();
            HandleZoomInput();
            HandleSprintFOV();
            UpdateCinemachineFOV();
            UpdateCrouchCamera();
            Move();
        }

        private void LateUpdate()
        {
            if (CinemachineCameraTarget != null)
                UpdateFirstPersonCamera();
        }

        // ============================================================================
        // CAMERA MANAGEMENT
        // ============================================================================

        private void CacheMainCameraMask()
        {
            if (_cachedMainCameraMask)
                return;

            var cam = GetMainCameraComponent();
            if (cam == null)
                return;

            _mainCameraBaseMask = cam.cullingMask;
            _cachedMainCameraMask = true;
        }

        private Camera GetMainCameraComponent()
        {
            if (_mainCamera == null)
                return null;

            if (_mainCamera.TryGetComponent<Camera>(out var cam))
                return cam;

            return _mainCamera.GetComponentInChildren<Camera>(true);
        }

        private void ApplyMainCameraCulling(bool hideMesh)
        {
            CacheMainCameraMask();

            var cam = GetMainCameraComponent();
            if (cam == null || !_cachedMainCameraMask)
                return;

            cam.cullingMask = hideMesh
                ? (_mainCameraBaseMask & ~playerBodyLayers.value)
                : _mainCameraBaseMask;
        }

        private void UpdateFirstPersonCamera()
        {
            // --- Return early if no look input ---
            if (_input.look.sqrMagnitude < _threshold)
                return;

            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _yaw += _input.look.x * deltaTimeMultiplier;
            _pitch += _input.look.y * deltaTimeMultiplier;

            _yaw = ClampAngle(_yaw, float.MinValue, float.MaxValue);
            _pitch = ClampAngle(_pitch, pitchMin, pitchMax);

            // --- Apply rotation based on body rotation mode ---
            if (rotateBodyYaw)
            {
                transform.rotation = Quaternion.Euler(0.0f, _yaw, 0.0f);
                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_pitch, 0.0f, 0.0f);
            }
            else
            {
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0.0f);
            }
        }

        // ============================================================================
        // ANIMATION
        // ============================================================================

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        // ============================================================================
        // GROUND DETECTION
        // ============================================================================

        private void GroundedCheck()
        {
            // --- Check if grounded using sphere cast ---
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (_hasAnimator)
                _animator.SetBool(_animIDGrounded, Grounded);
        }

        // ============================================================================
        // MOVEMENT
        // ============================================================================

        private void HandleSprintInput()
        {
            // --- Sprint is hold-based, but disable if crouching ---
            if (_isCrouching)
            {
                _input.sprint = false;
            }
            else
            {
                _input.sprint = Input.GetKey(sprintKey);
            }
        }

        private void Move()
        {
            // --- Calculate target speed based on state ---
            float targetSpeed = WalkSpeed;

            if (_input.sprint)
                targetSpeed = SprintSpeed;
            else if (_isCrouching)
                targetSpeed = crouchWalkSpeed;

            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // --- Smoothly transition to target speed ---
            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // --- Calculate movement direction (body-relative for FPS) ---
            Vector3 moveDirectionWorld = transform.right * _input.move.x + transform.forward * _input.move.y;

            // --- Apply movement with gravity ---
            _controller.Move(
                moveDirectionWorld.normalized * (_speed * Time.deltaTime) +
                new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime
            );

            // --- Update animations ---
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        // ============================================================================
        // JUMP AND GRAVITY
        // ============================================================================

        private void HandleJumpInput()
        {
            // --- Detect jump key press ---
            if (Input.GetKeyDown(jumpKey))
            {
                _input.jump = true;
            }
        }

        private void ApplyJumpAndGravity()
        {
            if (Grounded)
            {
                // --- Reset animation states when grounded ---
                if (_hasAnimator)
                    _animator.SetBool(_animIDJump, false);

                // --- Keep a small negative velocity to stay grounded ---
                if (_verticalVelocity < 0.0f)
                    _verticalVelocity = -2f;

                // --- Handle jump input with cooldown ---
                if (_input.jump && _jumpCooldownDelta <= 0.0f)
                {
                    _verticalVelocity = JumpForce;
                    if (_hasAnimator)
                        _animator.SetBool(_animIDJump, true);
                }

                // --- Decrement jump cooldown ---
                if (_jumpCooldownDelta >= 0.0f)
                    _jumpCooldownDelta -= Time.deltaTime;
            }
            else
            {
                // --- Airborne - reset jump cooldown and apply gravity ---
                _jumpCooldownDelta = JumpCooldown;
                _input.jump = false;
            }

            // --- Apply gravity using project default ---
            if (_verticalVelocity < _terminalVelocity)
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }

        // ============================================================================
        // CROUCH
        // ============================================================================

        private void HandleCrouchInput()
        {
            // --- Return early if crouch disabled ---
            if (!enableCrouch)
                return;

            // --- Toggle crouch on key press, but prevent if sprinting ---
            if (Input.GetKeyDown(crouchKey) && !_input.sprint)
            {
                _isCrouching = !_isCrouching;

                // --- Update character controller height ---
                _controller.height = _isCrouching ? crouchingHeight : standingHeight;

                // --- Adjust center to keep character grounded ---
                Vector3 newCenter = _controller.center;
                newCenter.y = _isCrouching ? crouchingHeight / 2.0f : standingHeight / 2.0f;
                _controller.center = newCenter;

                // --- Play crouch audio ---
                if (crouchAudioClips.Length > 0)
                {
                    var index = Random.Range(0, crouchAudioClips.Length);
                    AudioSource.PlayClipAtPoint(
                        crouchAudioClips[index],
                        transform.TransformPoint(_controller.center),
                        crouchAudioVolume
                    );
                }
            }
        }

        private void UpdateCrouchCamera()
        {
            // --- Return early if no camera target or crouch disabled ---
            if (CinemachineCameraTarget == null || !enableCrouch)
                return;

            // --- Calculate target camera Y offset ---
            float targetYOffset = _isCrouching ? crouchCameraOffset : defaultCameraHeightOffset;

            // --- Smoothly lerp camera offset ---
            _currentCameraYOffset = Mathf.Lerp(_currentCameraYOffset, targetYOffset, Time.deltaTime * crouchLerpSpeed);

            // --- Update camera position with lerped Y offset ---
            Vector3 newPos = _cameraTargetLocalPos;
            newPos.y = _cameraTargetLocalPos.y + _currentCameraYOffset;
            CinemachineCameraTarget.transform.localPosition = newPos;
        }

        // ============================================================================
        // ZOOM - HOLD BASED
        // ============================================================================

        private void HandleZoomInput()
        {
            // --- Return early if zoom disabled or no camera ---
            if (!enableZoom || firstPersonCamera == null)
                return;

            // --- Disable zoom while sprinting ---
            if (_input.sprint)
            {
                if (_isZooming)
                {
                    _isZooming = false;
                    _targetVerticalFOV = _input.sprint ? sprintFOV : normalFOV;
                }
                return;
            }

            // --- Check for zoom input using mouse button (hold-based) ---
            bool isZoomPressed = Input.GetMouseButton(1);

            // --- Handle zoom state changes ---
            if (isZoomPressed && !_isZooming)
            {
                _isZooming = true;
                _targetVerticalFOV = zoomFOV;
            }
            else if (!isZoomPressed && _isZooming)
            {
                _isZooming = false;
                _targetVerticalFOV = normalFOV;
            }
        }

        // ============================================================================
        // SPRINT FOV
        // ============================================================================

        private void HandleSprintFOV()
        {
            // --- Return early if sprint FOV disabled or no camera ---
            if (!enableSprintFOV || firstPersonCamera == null)
                return;

            // --- Only apply sprint FOV if not zooming ---
            if (_isZooming)
                return;

            // --- Check if player is moving and sprinting ---
            bool isMoving = _input.move.sqrMagnitude > _threshold;
            bool isSprinting = _input.sprint && isMoving;

            // --- Determine target FOV based on sprint state and movement ---
            if (isSprinting)
            {
                _targetVerticalFOV = sprintFOV;
            }
            else
            {
                _targetVerticalFOV = normalFOV;
            }
        }

        // ============================================================================
        // CINEMACHINE FOV SMOOTHING
        // ============================================================================

        private void UpdateCinemachineFOV()
        {
            // --- Return early if no Cinemachine camera ---
            if (firstPersonCamera == null)
                return;

            // --- Smooth FOV transition to target ---
            _currentVerticalFOV = Mathf.Lerp(_currentVerticalFOV, _targetVerticalFOV, Time.deltaTime * zoomSmoothSpeed);

            // --- Update the Cinemachine camera's field of view ---
            var lens = firstPersonCamera.Lens;
            lens.FieldOfView = _currentVerticalFOV;
            firstPersonCamera.Lens = lens;
        }

        // ============================================================================
        // PUBLIC API
        // ============================================================================

        /// <summary>
        /// External systems can call this to apply an impulse to the player
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            _verticalVelocity = velocity.y;
            Grounded = false;

            if (_hasAnimator)
                _animator.SetBool(_animIDJump, true);
        }

        /// <summary>
        /// Toggles whether the player mesh is visible in first person
        /// </summary>
        public void SetMeshVisibility(bool visible)
        {
            showPlayerMesh = visible;
            ApplyMainCameraCulling(!visible);
        }

        // ============================================================================
        // UTILITIES
        // ============================================================================

        private static float ClampAngle(float angle, float min, float max)
        {
            // --- Normalize angle to -360 to 360 range ---
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }

        // ============================================================================
        // DEBUG
        // ============================================================================

        private void OnDrawGizmosSelected()
        {
            // --- Draw ground check sphere ---
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color = Grounded ? transparentGreen : transparentRed;
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius
            );
        }

        // ============================================================================
        // ANIMATION EVENTS
        // ============================================================================

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(
                        FootstepAudioClips[index],
                        transform.TransformPoint(_controller.center),
                        FootstepAudioVolume
                    );
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(
                    LandingAudioClip,
                    transform.TransformPoint(_controller.center),
                    FootstepAudioVolume
                );
            }
        }
    }
}