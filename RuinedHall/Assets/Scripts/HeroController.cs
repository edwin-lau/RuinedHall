using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class HeroController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 6.4f;
    [SerializeField] float runSpeed = 11.1f;
    [SerializeField] float rotateSpeed = 720f;
    [SerializeField] float jumpHeight = 2.45f;
    [SerializeField] float gravity = -78f;
    [SerializeField] float walkToRunDelay = 1.5f;
    [SerializeField] float moveDeadZone = 0.18f;
    [SerializeField] float startAcceleration = 42f;
    [SerializeField] float stopDeceleration = 26f;
    [SerializeField] float runStaminaDrainPerSecond = 0.2f;
    [SerializeField] float runStaminaRecoveryDelay = 2f;
    const float RunStaminaFull = 0.999f;
    const float ReferenceHeight = 1.8f;
    [SerializeField] int maxHealth = 200;
    [SerializeField] int punchDamage = 20;
    [SerializeField] float punchRange = 0.84f;
    const float PunchArcDegrees = 120f;

    CharacterController _controller;
    Animator _animator;
    Vector3 _fittedLossyScale;
    Transform _cameraTransform;
    VirtualJoystick _joystick;
    MobileActionButton _punchButton;
    HitKnockback _knockback;
    PunchRangeDisplay _punchRange;
    Vector2 _moveInput;
    Vector3 _verticalVelocity;
    float _moveHoldTime;
    float _currentSpeed;
    Vector3 _coastDirection;
    float _runStamina = 1f;
    float _notRunningSince = -1f;
    bool _running;
    float _actionUntil;
    int _comboIndex;
    string _playingState;
    int _currentHealth;
    bool _punchHitApplied;
    bool _punchConnected;
    bool _punchQueued;
    bool _comboReady;
    bool _isDead;
    bool _jumpLeftGround;
    bool _jumpLocksUntilAnim;
    Vector3 _jumpRootDelta;
    ActionKind _action;
    static readonly string[] ComboStates = { "Punch1", "Punch2", "Punch3", "Punch4" };
    const float ComboHitNormalized = 0.38f;
    const float ComboChainNormalized = 0.5f;

    enum ActionKind
    {
        None,
        Jump,
        Punch,
        Hit,
        Death
    }

    public static HeroController Active { get; private set; }

    void OnEnable()
    {
        Active = this;
    }

    void OnDisable()
    {
        if (Active == this)
            Active = null;
        if (_punchRange != null)
            _punchRange.Hide();
    }

    void OnDestroy()
    {
        if (_punchRange != null)
            Destroy(_punchRange.gameObject);
    }

    public bool IsDead => _isDead;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public float RunStamina => _runStamina;
    public bool CanRun => _runStamina >= RunStaminaFull;
    public float WorldHeight => CharacterBodyFit.WorldHeight(transform);
    public float PlanarSpeed => _currentSpeed;
    float _motionScaleOverride;
    float MotionScale => _motionScaleOverride > 0f
        ? _motionScaleOverride
        : Mathf.Max(0.2f, WorldHeight / ReferenceHeight);

    public void CopyTuningFrom(HeroController other)
    {
        if (other == null || other == this)
            return;
        walkSpeed = other.walkSpeed;
        runSpeed = other.runSpeed;
        rotateSpeed = other.rotateSpeed;
        jumpHeight = other.jumpHeight;
        gravity = other.gravity;
        walkToRunDelay = other.walkToRunDelay;
        startAcceleration = other.startAcceleration;
        stopDeceleration = other.stopDeceleration;
        punchRange = 0.84f;
    }

    public void SetMoveSpeeds(float walk, float run)
    {
        if (walk > 0f)
            walkSpeed = walk;
        if (run > 0f)
            runSpeed = run;
    }

    public void LockMotionToWorldHeight(float worldHeight)
    {
        if (worldHeight < 0.2f)
            return;
        _motionScaleOverride = worldHeight / ReferenceHeight;
        _fittedLossyScale = transform.lossyScale;
    }
    public event Action<int, int> HealthChanged;
    public event Action<float> RunStaminaChanged;
    public event Action Died;

    public void SetJoystick(VirtualJoystick joystick)
    {
        _joystick = joystick;
    }

    public void SetPunchButton(MobileActionButton punchButton)
    {
        _punchButton = punchButton;
    }

    public void SetMoveInput(Vector2 input)
    {
        _moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void Punch()
    {
        if (_isDead || _action == ActionKind.Hit)
            return;
        if (_action == ActionKind.Jump && _jumpLocksUntilAnim)
            return;
        if (_action == ActionKind.Punch)
        {
            _punchQueued = true;
            return;
        }
        if (_action == ActionKind.Jump && !_controller.isGrounded)
            return;

        BeginPunch(0);
    }

    public void Jump()
    {
        if (_isDead || !_controller.isGrounded || _action == ActionKind.Jump || _action == ActionKind.Hit || _action == ActionKind.Punch)
            return;

        _action = ActionKind.Jump;
        _jumpLeftGround = false;
        string jumpState = ResolveJumpState();
        SetTravelJumpRootMotion(jumpState == "WalkJump" || jumpState == "RunJump");
        _verticalVelocity.y = Mathf.Sqrt(jumpHeight * MotionScale * -2f * gravity);
        _actionUntil = Time.time + PlayAndMeasure(jumpState, 0.04f, true);
    }

    public void Die()
    {
        if (_isDead)
            return;

        _isDead = true;
        _action = ActionKind.Death;
        _moveInput = Vector2.zero;
        _verticalVelocity = Vector3.zero;
        _currentSpeed = 0f;
        _coastDirection = Vector3.zero;
        ResetPunchCombo();
        SetTravelJumpRootMotion(false);
        PlayState("Death", 0.08f);
        Died?.Invoke();
    }

    public void Revive()
    {
        if (!_isDead)
            return;

        _isDead = false;
        _action = ActionKind.None;
        _actionUntil = 0f;
        ResetPunchCombo();
        _punchHitApplied = true;
        _jumpLeftGround = false;
        SetTravelJumpRootMotion(false);
        _moveInput = Vector2.zero;
        _verticalVelocity = Vector3.zero;
        _currentSpeed = 0f;
        _coastDirection = Vector3.zero;
        _currentHealth = maxHealth;
        _runStamina = 1f;
        _notRunningSince = -1f;
        _running = false;
        NotifyHealth();
        NotifyRunStamina();
        PlayState("Idle", 0.08f, true);
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position + transform.forward);
    }

    public void TakeDamage(int amount, Vector3 hitOrigin)
    {
        if (_isDead || amount <= 0)
            return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        NotifyHealth();
        HitKnockback.ApplyTo(this, hitOrigin, _currentHealth == 0 ? 1.35f : 1f);
        _knockback = GetComponent<HitKnockback>();
        if (_currentHealth == 0)
        {
            Die();
            return;
        }

        ResetPunchCombo();
        SetTravelJumpRootMotion(false);
        _action = ActionKind.Hit;
        _actionUntil = Time.time + PlayAndMeasure("Hit", 0.05f, true);
    }

    void Awake()
    {
        punchRange = 0.84f;
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _knockback = GetComponent<HitKnockback>();
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        FitBodyToCurrentScale();
        _currentHealth = maxHealth;
        _runStamina = 1f;
        _notRunningSince = -1f;
        _running = false;
        NotifyHealth();
        NotifyRunStamina();
    }

    void NotifyHealth()
    {
        HealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    void NotifyRunStamina()
    {
        RunStaminaChanged?.Invoke(_runStamina);
    }

    void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;

        PlayState("Idle", 0f);
    }

    void Update()
    {
        _moveInput = _joystick != null ? _joystick.Value : Vector2.zero;
        ReadKeyboard();

        if (_isDead)
        {
            ApplyGravityOnly();
            return;
        }

        UpdatePunchCombo();

        bool finishTravelJump = false;
        if (_action == ActionKind.Jump)
        {
            if (!_controller.isGrounded)
                _jumpLeftGround = true;
            else if (_jumpLocksUntilAnim)
            {
                if (CurrentNormalizedTime() >= 1f)
                    finishTravelJump = true;
            }
            else if (_jumpLeftGround && _verticalVelocity.y <= 0f)
                _action = ActionKind.None;
        }

        if (_action != ActionKind.None &&
            Time.time >= _actionUntil &&
            _action != ActionKind.Jump &&
            _action != ActionKind.Punch)
        {
            bool fromHit = _action == ActionKind.Hit;
            _action = ActionKind.None;
            if (fromHit)
                PlayState("Idle", 0.08f, true);
        }

        if (_action == ActionKind.Jump &&
            !_jumpLocksUntilAnim &&
            Time.time >= _actionUntil &&
            _controller.isGrounded)
            _action = ActionKind.None;

        Vector3 planar = CameraRelativeMove();
        if (_knockback == null)
            _knockback = GetComponent<HitKnockback>();
        if (_knockback != null && _knockback.IsActive)
            planar *= 0.12f;
        if (_action == ActionKind.Hit)
            planar *= 0.08f;
        bool punching = _action == ActionKind.Punch;
        if (punching)
            planar = Vector3.zero;
        bool travelJump = _action == ActionKind.Jump && _jumpLocksUntilAnim;
        if (travelJump)
            planar = Vector3.zero;
        bool moving = planar.sqrMagnitude >= moveDeadZone * moveDeadZone;
        float scale = MotionScale;
        float walk = walkSpeed * scale;
        float run = runSpeed * scale;

        if (moving)
        {
            _moveHoldTime += Time.deltaTime;
            _coastDirection = planar.normalized;
            Quaternion look = Quaternion.LookRotation(_coastDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, rotateSpeed * Time.deltaTime);

            if (!_running && _moveHoldTime >= walkToRunDelay && _runStamina >= RunStaminaFull)
                _running = true;

            float targetSpeed = _running ? run : walk;
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed, targetSpeed, startAcceleration * scale * Time.deltaTime);
        }
        else
        {
            _moveHoldTime = 0f;
            _running = false;
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed, 0f, stopDeceleration * scale * Time.deltaTime);
            if (_currentSpeed <= 0.05f)
            {
                _currentSpeed = 0f;
                _coastDirection = Vector3.zero;
            }
        }

        if (_running)
        {
            _runStamina = Mathf.Max(0f, _runStamina - runStaminaDrainPerSecond * Time.deltaTime);
            _notRunningSince = -1f;
            if (_runStamina <= 0f)
                _running = false;
        }
        else
        {
            if (_notRunningSince < 0f)
                _notRunningSince = Time.time;
            else if (Time.time - _notRunningSince >= runStaminaRecoveryDelay)
                _runStamina = 1f;
        }

        NotifyRunStamina();

        if (_controller.isGrounded && _verticalVelocity.y < 0f)
            _verticalVelocity.y = -2f;
        _verticalVelocity.y += gravity * Time.deltaTime;

        if (travelJump)
        {
            Vector3 root = _jumpRootDelta;
            root.y = 0f;
            _jumpRootDelta = Vector3.zero;
            _controller.Move(root + _verticalVelocity * Time.deltaTime);
        }
        else
        {
            Vector3 motion = !punching && _currentSpeed > 0.05f && _coastDirection.sqrMagnitude > 0.01f
                ? _coastDirection * _currentSpeed
                : Vector3.zero;
            motion += _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        if (finishTravelJump)
        {
            _action = ActionKind.None;
            SetTravelJumpRootMotion(false);
        }

        if (_action == ActionKind.None)
        {
            if (_currentSpeed <= walk * 0.2f)
                PlayState("Idle", 0.28f);
            else if (_running)
                PlayLocomotion("Run");
            else
                PlayLocomotion("Walk");
        }
    }

    void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 keys = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            keys.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            keys.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            keys.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            keys.x -= 1f;

        if (keys.sqrMagnitude > 0.01f)
            _moveInput = Vector2.ClampMagnitude(_moveInput + keys.normalized, 1f);

        if (keyboard.spaceKey.wasPressedThisFrame)
            Jump();
        if (keyboard.jKey.wasPressedThisFrame)
            Punch();
        if (keyboard.kKey.wasPressedThisFrame)
            Die();
    }

    Vector3 CameraRelativeMove()
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (_cameraTransform != null)
        {
            forward = _cameraTransform.forward;
            right = _cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        return right * _moveInput.x + forward * _moveInput.y;
    }

    void ApplyGravityOnly()
    {
        if (_controller.isGrounded && _verticalVelocity.y < 0f)
            _verticalVelocity.y = -2f;
        _verticalVelocity.y += gravity * Time.deltaTime;
        _controller.Move(_verticalVelocity * Time.deltaTime);
    }

    void PlayLocomotion(string stateName)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;
        if (_playingState == stateName)
            return;
        if (!_animator.HasState(0, Animator.StringToHash(stateName)))
            return;

        bool fromLocomotion = _playingState == "Walk" || _playingState == "Run";
        float phase = 0f;
        if (fromLocomotion)
        {
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            phase = info.normalizedTime - Mathf.Floor(info.normalizedTime);
        }

        bool fromRun = _playingState == "Run";
        _playingState = stateName;
        if (!fromLocomotion)
        {
            _animator.CrossFadeInFixedTime(stateName, 0.12f, 0, 0f);
            return;
        }

        float destLength = PlaybackLength();
        float fade = fromRun && stateName == "Walk" ? 0.28f : 0.2f;
        _animator.CrossFadeInFixedTime(stateName, fade, 0, destLength > 0f ? phase * destLength : 0f);
    }

    void PlayState(string stateName, float fade = 0.12f, bool restart = false)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;
        if (!restart && _playingState == stateName)
            return;
        if (!_animator.HasState(0, Animator.StringToHash(stateName)))
            return;

        _playingState = stateName;
        if (restart || fade <= 0f)
            _animator.Play(stateName, 0, 0f);
        else
            _animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
    }

    string ResolveJumpState()
    {
        if (_running && HasAnimState("RunJump"))
            return "RunJump";
        bool walking = _currentSpeed > walkSpeed * MotionScale * 0.2f ||
            _moveInput.sqrMagnitude >= moveDeadZone * moveDeadZone;
        if (walking && HasAnimState("WalkJump"))
            return "WalkJump";
        if (HasAnimState("Jump"))
            return "Jump";
        if (HasAnimState("WalkJump"))
            return "WalkJump";
        return "RunJump";
    }

    float PlayAndMeasure(string stateName, float fade, bool restart)
    {
        PlayState(stateName, fade, restart);
        if (_animator != null)
            _animator.Update(0f);
        return PlaybackLength();
    }

    float PlaybackLength()
    {
        return AnimPlayback.Length(_animator);
    }

    float CurrentNormalizedTime()
    {
        return AnimPlayback.NormalizedTime(_animator);
    }

    void OnAnimatorMove()
    {
        if (_action == ActionKind.Jump && _jumpLocksUntilAnim && _animator != null)
            _jumpRootDelta = _animator.deltaPosition;
    }

    void LateUpdate()
    {
        if (transform.lossyScale != _fittedLossyScale)
            FitBodyToCurrentScale();

        LockLocomotionSway();
        UpdatePunchRangeVisual();
    }

    void SetTravelJumpRootMotion(bool on)
    {
        if (on)
        {
            _jumpLocksUntilAnim = true;
            _jumpRootDelta = Vector3.zero;
            if (_animator != null)
                _animator.applyRootMotion = true;
            return;
        }

        if (_jumpLocksUntilAnim)
            CommitJumpDisplacement();
        _jumpLocksUntilAnim = false;
        _jumpRootDelta = Vector3.zero;
        if (_animator != null)
            _animator.applyRootMotion = false;
    }

    void CommitJumpDisplacement()
    {
        if (_controller == null || _animator == null || !_animator.isHuman)
            return;

        Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
        Vector3 visual = hips != null ? hips.position : _animator.rootPosition;
        Vector3 delta = visual - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= 0.0001f)
            return;

        _controller.Move(delta);
        if (hips == null)
            return;

        Vector3 local = transform.InverseTransformPoint(hips.position);
        local.x = 0f;
        local.z = 0f;
        hips.position = transform.TransformPoint(local);
    }

    void UpdatePunchRangeVisual()
    {
        if (_punchRange == null)
            _punchRange = PunchRangeDisplay.Create();

        if (_action != ActionKind.Punch)
        {
            _punchRange.Hide();
            return;
        }

        _punchRange.Show(
            transform.position,
            transform.forward,
            PunchRadius(),
            PunchArcDegrees,
            _punchHitApplied,
            _punchConnected);
    }

    float PunchRadius()
    {
        return punchRange * MotionScale;
    }

    void LockLocomotionSway()
    {
        if (_animator == null || !_animator.isHuman)
            return;
        if (_playingState != "Walk" && _playingState != "Run" && _action != ActionKind.Punch)
            return;

        _animator.applyRootMotion = false;

        Vector3 root = _animator.rootPosition;
        root.x = transform.position.x;
        root.z = transform.position.z;
        _animator.rootPosition = root;
        _animator.rootRotation = transform.rotation;

        Vector3 body = _animator.bodyPosition;
        body.x = transform.position.x;
        body.z = transform.position.z;
        _animator.bodyPosition = body;

        Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
            return;

        Vector3 local = transform.InverseTransformPoint(hips.position);
        local.x = 0f;
        if (_action == ActionKind.Punch)
            local.z = 0f;
        hips.position = transform.TransformPoint(local);
    }

    public void FitBodyToCurrentScale()
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
        CharacterBodyFit.Apply(_controller, transform);
        _fittedLossyScale = transform.lossyScale;
    }

    void BeginPunch(int comboIndex)
    {
        _comboIndex = comboIndex;
        string state = PunchState(comboIndex);
        _action = ActionKind.Punch;
        _punchHitApplied = false;
        _punchConnected = false;
        _punchQueued = false;
        _comboReady = false;
        _currentSpeed = 0f;
        _coastDirection = Vector3.zero;
        _running = false;
        _moveHoldTime = 0f;
        float clipLength = PlayAndMeasure(state, 0.04f, true);
        _actionUntil = Time.time + clipLength;
    }

    void UpdatePunchCombo()
    {
        if (_action != ActionKind.Punch)
            return;

        float normalized = CurrentNormalizedTime();
        if (!_punchHitApplied && normalized >= ComboHitNormalized)
            ApplyPunchDamage();

        int next = _comboIndex + 1;
        bool canChain = _punchHitApplied &&
            _punchConnected &&
            next < ComboStates.Length &&
            PunchState(next) != PunchState(_comboIndex) &&
            (_punchQueued || IsPunchHeld());

        if (!_comboReady && normalized >= ComboChainNormalized && canChain)
            _comboReady = true;

        if (normalized < 1f)
            return;

        if (_comboReady)
        {
            BeginPunch(next);
            return;
        }

        ResetPunchCombo();
        _action = ActionKind.None;
        _actionUntil = 0f;
    }

    string PunchState(int comboIndex)
    {
        if (comboIndex < 0 || comboIndex >= ComboStates.Length)
            comboIndex = 0;
        if (HasAnimState(ComboStates[comboIndex]))
            return ComboStates[comboIndex];
        return ComboStates[0];
    }

    bool HasAnimState(string stateName)
    {
        return _animator != null && _animator.HasState(0, Animator.StringToHash(stateName));
    }

    bool IsPunchHeld()
    {
        if (_punchButton != null && _punchButton.IsHeld)
            return true;

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.jKey.isPressed;
    }

    void ResetPunchCombo()
    {
        _comboIndex = 0;
        _punchConnected = false;
        _punchHitApplied = true;
        _punchQueued = false;
        _comboReady = false;
    }

    void ApplyPunchDamage()
    {
        _punchHitApplied = true;
        float radius = PunchRadius();
        Vector3 origin = transform.position + Vector3.up * (WorldHeight * 0.5f);
        Collider[] hits = Physics.OverlapSphere(
            origin,
            radius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        var struck = new HashSet<CharacterCombatAgent>();
        foreach (Collider hit in hits)
        {
            CharacterCombatAgent enemy = hit.GetComponentInParent<CharacterCombatAgent>();
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3 targetPoint = hit.bounds.center;
            if (!InPunchArc(targetPoint, radius) && !InPunchArc(enemy.transform.position, radius))
                continue;

            if (!struck.Add(enemy))
                continue;

            enemy.TakeDamage(punchDamage, transform.position);
        }

        _punchConnected = struck.Count > 0;
    }

    bool InPunchArc(Vector3 worldPoint, float radius)
    {
        Vector3 to = worldPoint - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance > radius || distance < 0.01f)
            return false;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return true;
        return Vector3.Angle(forward, to) <= PunchArcDegrees * 0.5f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position;
        float radius = PunchRadius();
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();
        int steps = 16;
        Vector3 prev = origin;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float angle = Mathf.Lerp(-PunchArcDegrees * 0.5f, PunchArcDegrees * 0.5f, t);
            Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * (forward * radius);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
        Gizmos.DrawLine(prev, origin);
    }
}
