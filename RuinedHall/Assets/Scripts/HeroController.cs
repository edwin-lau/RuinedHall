using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家英雄控制器：镜头相对移动、跑步耐力、跳跃、拳击连招、受击与死亡。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class HeroController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 1.45f;
    [SerializeField] float runSpeed = 3.35f;
    [SerializeField] float rotateSpeed = 720f;
    [SerializeField] float jumpHeight = 2.45f;
    [SerializeField] float gravity = -78f;
    [SerializeField] float walkToRunDelay = 1.5f;
    [SerializeField] float moveDeadZone = 0.18f;
    [SerializeField] float startAcceleration = 32f;
    [SerializeField] float stopDeceleration = 22f;
    [SerializeField] float jumpTakeoffEnd = 0.22f;
    [SerializeField] float jumpAirborneHold = 0.38f;
    [SerializeField] float jumpLandingStart = 0.72f;
    [SerializeField] float landingMinDuration = 0.28f;
    [SerializeField] float landingMoveScale = 0.22f;
    [SerializeField] float landingSpeedRetention = 0.42f;
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
    JumpPhase _jumpPhase;
    Vector3 _jumpRootDelta;
    float _hitHipLocalY = -1f;
    ActionKind _action;
    static readonly string[] ComboStates = { "Punch1", "Punch2", "Punch3", "Punch4" };
    const float ComboHitNormalized = 0.38f;
    const float ComboChainNormalized = 0.5f;
    const float HitMinDuration = 0.35f;

    /// <summary>当前动作类型：待机移动、跳、拳、受击或死亡。</summary>
    enum ActionKind
    {
        None,
        Jump,
        Punch,
        Hit,
        Death
    }

    /// <summary>跳跃表现阶段：起跳、腾空、落地收势。</summary>
    enum JumpPhase
    {
        None,
        Takeoff,
        Airborne,
        Landing
    }

    /// <summary>当前场景中激活的英雄。</summary>
    public static HeroController Active { get; private set; }

    // 把自己登记为当前激活英雄。
    void OnEnable()
    {
        Active = this;
    }

    // 取消激活登记并隐藏出拳范围。
    void OnDisable()
    {
        if (Active == this)
            Active = null;
        if (_punchRange != null)
            _punchRange.Hide();
    }

    // 销毁出拳范围可视化物体。
    void OnDestroy()
    {
        if (_punchRange != null)
            Destroy(_punchRange.gameObject);
    }

    /// <summary>英雄是否已死亡。</summary>
    public bool IsDead => _isDead;
    /// <summary>当前生命值。</summary>
    public int CurrentHealth => _currentHealth;
    /// <summary>最大生命值。</summary>
    public int MaxHealth => maxHealth;
    /// <summary>跑步耐力，1 为满。</summary>
    public float RunStamina => _runStamina;
    /// <summary>耐力是否足够开始奔跑。</summary>
    public bool CanRun => _runStamina >= RunStaminaFull;
    /// <summary>角色世界身高。</summary>
    public float WorldHeight => CharacterBodyFit.WorldHeight(transform);
    /// <summary>当前平面移动速度。</summary>
    public float PlanarSpeed => _currentSpeed;
    /// <summary>是否正在奔跑。</summary>
    public bool IsRunning => _running;
    /// <summary>是否贴地。</summary>
    public bool IsGrounded => _controller != null && _controller.isGrounded;
    /// <summary>当前播放的动画状态名。</summary>
    public string LocomotionState => _playingState;
    /// <summary>是否处于走/跑位移（非战斗/跳跃）。</summary>
    public bool IsInLocomotion =>
        _action == ActionKind.None &&
        (_playingState == "Walk" || _playingState == "Run");
    /// <summary>是否允许脚步系统接管走跑动画速率。</summary>
    public bool AllowsLocomotionAnimSpeed =>
        _action == ActionKind.None &&
        (_playingState == "Walk" || _playingState == "Run");
    /// <summary>跳跃腾空/落地阶段由控制器自行管理动画速率。</summary>
    public bool OverridesAnimSpeed =>
        _action == ActionKind.Jump &&
        (_jumpPhase == JumpPhase.Airborne || _jumpPhase == JumpPhase.Landing);
    float _motionScaleOverride;
    /// <summary>动作位移相对 1.8 米参考身高的缩放。</summary>
    public float MotionScale => _motionScaleOverride > 0f
        ? _motionScaleOverride
        : Mathf.Max(0.2f, WorldHeight / ReferenceHeight);

    /// <summary>从另一份英雄配置复制移动相关参数。</summary>
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

    /// <summary>覆盖行走与奔跑速度。</summary>
    public void SetMoveSpeeds(float walk, float run)
    {
        if (walk > 0f)
            walkSpeed = walk;
        if (run > 0f)
            runSpeed = run;
    }

    /// <summary>按给定世界身高锁定动作缩放。</summary>
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

    /// <summary>绑定虚拟摇杆。</summary>
    public void SetJoystick(VirtualJoystick joystick)
    {
        _joystick = joystick;
    }

    /// <summary>绑定移动端出拳按钮。</summary>
    public void SetPunchButton(MobileActionButton punchButton)
    {
        _punchButton = punchButton;
    }

    /// <summary>设置平面移动输入。</summary>
    public void SetMoveInput(Vector2 input)
    {
        _moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>尝试出拳；连招窗口内则排队下一击。</summary>
    public void Punch()
    {
        if (_isDead || _action == ActionKind.Hit)
            return;
        if (_action == ActionKind.Jump && _jumpLocksUntilAnim)
            return;
        if (_action == ActionKind.Punch)
        {
            // 当前正在出拳则排队，等本段结束再衔接。
            _punchQueued = true;
            return;
        }
        if (_action == ActionKind.Jump && !_controller.isGrounded)
            return;

        BeginPunch(0);
    }

    /// <summary>在地面且无冲突动作时起跳。</summary>
    public void Jump()
    {
        if (_isDead || !_controller.isGrounded || _action == ActionKind.Jump || _action == ActionKind.Hit || _action == ActionKind.Punch)
            return;

        _action = ActionKind.Jump;
        _jumpPhase = JumpPhase.Takeoff;
        _jumpLeftGround = false;
        string jumpState = ResolveJumpState();
        if (jumpState == "Jump" && HasAnimState("JumpStart"))
            jumpState = "JumpStart";
        SetTravelJumpRootMotion(jumpState == "WalkJump" || jumpState == "RunJump");
        _verticalVelocity.y = Mathf.Sqrt(jumpHeight * MotionScale * -2f * gravity);
        _actionUntil = Time.time + PlayAndMeasure(jumpState, 0.04f, true);
    }

    /// <summary>进入死亡状态并播放死亡动画。</summary>
    public void Die()
    {
        if (_isDead)
            return;

        _isDead = true;
        _action = ActionKind.Death;
        _jumpPhase = JumpPhase.None;
        _moveInput = Vector2.zero;
        _verticalVelocity = Vector3.zero;
        _currentSpeed = 0f;
        _coastDirection = Vector3.zero;
        ResetPunchCombo();
        SetTravelJumpRootMotion(false);
        PlayState("Death", 0.08f);
        Died?.Invoke();
    }

    /// <summary>复活并重置战斗与移动状态。</summary>
    public void Revive()
    {
        if (!_isDead)
            return;

        _isDead = false;
        _action = ActionKind.None;
        _jumpPhase = JumpPhase.None;
        _actionUntil = 0f;
        ResetPunchCombo();
        _punchHitApplied = true;
        _jumpLeftGround = false;
        _jumpPhase = JumpPhase.None;
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

    /// <summary>从身前默认来源扣血。</summary>
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position + transform.forward);
    }

    /// <summary>扣血并进入受击或死亡。</summary>
    public void TakeDamage(int amount, Vector3 hitOrigin)
    {
        if (_isDead || amount <= 0)
            return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        NotifyHealth();
        HitKnockback.ApplyTo(this, hitOrigin, _currentHealth == 0 ? 1.35f : 1f, 0f);
        _knockback = GetComponent<HitKnockback>();
        if (_currentHealth == 0)
        {
            Die();
            return;
        }

        // 未致死则打断连招并进入受击硬直。
        ResetPunchCombo();
        SetTravelJumpRootMotion(false);
        _jumpPhase = JumpPhase.None;
        _moveHoldTime = 0f;
        _currentSpeed = 0f;
        _coastDirection = Vector3.zero;
        _running = false;
        _verticalVelocity.y = 0f;
        CacheStandingHip();
        _action = ActionKind.Hit;
        float hitLength = PlayAndMeasure("Hit", 0.05f, true);
        _actionUntil = Time.time + Mathf.Max(HitMinDuration, hitLength);
    }

    // 初始化控制器、动画与生命值。
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

    // 广播当前生命值。
    void NotifyHealth()
    {
        HealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // 广播当前跑步耐力。
    void NotifyRunStamina()
    {
        RunStaminaChanged?.Invoke(_runStamina);
    }

    // 缓存主相机并切入待机。
    void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;

        PlayState("Idle", 0f);
    }

    // 每帧处理输入、动作状态、移动与动画。
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
        UpdateHitReaction();

        // 跳跃阶段：起跳 → 腾空 → 落地收势。
        bool finishTravelJump = false;
        if (_action == ActionKind.Jump)
            finishTravelJump = UpdateJumpAction();

        // 非跳跃/出拳/受击的限时动作到期后清掉。
        if (_action != ActionKind.None &&
            Time.time >= _actionUntil &&
            _action != ActionKind.Jump &&
            _action != ActionKind.Punch &&
            _action != ActionKind.Hit)
        {
            _action = ActionKind.None;
        }

        if (_action == ActionKind.Jump &&
            _jumpPhase != JumpPhase.Landing &&
            !_jumpLocksUntilAnim &&
            Time.time >= _actionUntil &&
            _controller.isGrounded &&
            _jumpLeftGround)
            EnterJumpLanding();

        // 受击、出拳、位移跳时锁平面移动；击退中也大幅削弱走位。
        Vector3 planar = CameraRelativeMove();
        if (_knockback == null)
            _knockback = GetComponent<HitKnockback>();
        if (_knockback != null && _knockback.IsActive)
            planar *= 0.12f;
        bool hitReacting = _action == ActionKind.Hit;
        if (hitReacting)
            planar = Vector3.zero;
        bool punching = _action == ActionKind.Punch;
        if (punching)
            planar = Vector3.zero;
        bool travelJump = _action == ActionKind.Jump && _jumpLocksUntilAnim;
        bool landingRecover = _action == ActionKind.Jump && _jumpPhase == JumpPhase.Landing;
        if (travelJump)
            planar = Vector3.zero;
        else if (landingRecover)
            planar *= landingMoveScale;
        bool moving = planar.sqrMagnitude >= moveDeadZone * moveDeadZone;
        float scale = MotionScale;
        float walk = walkSpeed * scale;
        float run = runSpeed * scale;

        // 有输入则转向并加速，无输入则减速滑行。
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

        // 奔跑消耗耐力；停下一段时间后一次回满。
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

        if (_knockback != null)
            _knockback.Tick(Time.deltaTime);

        // 受击时也落地贴地，其它情况正常施加重力。
        if (hitReacting)
        {
            if (_controller.isGrounded)
                _verticalVelocity.y = -2f;
            else
                _verticalVelocity.y += gravity * Time.deltaTime;
        }
        else
        {
            if (_controller.isGrounded && _verticalVelocity.y < 0f)
                _verticalVelocity.y = -2f;
            _verticalVelocity.y += gravity * Time.deltaTime;
        }

        // 位移跳用根运动；否则用速度 + 击退移动胶囊体。
        if (travelJump)
        {
            Vector3 root = _jumpRootDelta;
            root.y = 0f;
            _jumpRootDelta = Vector3.zero;
            _controller.Move(root + _verticalVelocity * Time.deltaTime);
        }
        else
        {
            Vector3 motion = !punching && !hitReacting && _currentSpeed > 0.05f && _coastDirection.sqrMagnitude > 0.01f
                ? _coastDirection * _currentSpeed
                : Vector3.zero;
            motion += _verticalVelocity;
            if (_knockback != null && _knockback.IsActive)
                motion += _knockback.Velocity;
            _controller.Move(motion * Time.deltaTime);
        }

        if (finishTravelJump)
            FinishJump();

        // 无特殊动作时按速度播待机/走/跑。
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

    // 推进跳跃三阶段；返回 true 表示位移跳可结束。
    bool UpdateJumpAction()
    {
        if (!_controller.isGrounded)
            _jumpLeftGround = true;

        switch (_jumpPhase)
        {
            case JumpPhase.Takeoff:
                UpdateJumpTakeoff();
                break;
            case JumpPhase.Airborne:
                UpdateJumpAirborne();
                break;
            case JumpPhase.Landing:
                UpdateJumpLanding();
                break;
        }

        if (!_jumpLocksUntilAnim)
            return false;

        if (_jumpPhase == JumpPhase.Landing)
            return false;

        if (_controller.isGrounded && _jumpLeftGround)
        {
            if (HasAnimState("Landing") && CurrentNormalizedTime() < 0.95f)
            {
                EnterJumpLanding();
                return false;
            }

            if (CurrentNormalizedTime() >= 1f)
                return true;
        }

        return false;
    }

    void UpdateJumpTakeoff()
    {
        if (_jumpLocksUntilAnim)
        {
            if (_jumpLeftGround || CurrentNormalizedTime() >= 0.34f)
                EnterJumpAirborne();
            return;
        }

        if (_jumpLeftGround || CurrentNormalizedTime() >= jumpTakeoffEnd)
            EnterJumpAirborne();
    }

    void UpdateJumpAirborne()
    {
        if (_jumpLocksUntilAnim)
            return;

        if (_controller.isGrounded && _jumpLeftGround && _verticalVelocity.y <= 0f)
            EnterJumpLanding();
    }

    void UpdateJumpLanding()
    {
        if (Time.time < _actionUntil && CurrentNormalizedTime() < 0.98f)
            return;
        FinishJump();
    }

    void EnterJumpAirborne()
    {
        if (_jumpPhase == JumpPhase.Airborne || _jumpPhase == JumpPhase.Landing)
            return;

        _jumpPhase = JumpPhase.Airborne;
        if (_jumpLocksUntilAnim)
            return;

        if (HasAnimState("Fall"))
        {
            PlayState("Fall", 0.1f);
            _actionUntil = float.PositiveInfinity;
            return;
        }

        if (!HasAnimState("Jump"))
            return;

        _playingState = "Airborne";
        if (_animator != null)
        {
            _animator.speed = 0f;
            _animator.Play("Jump", 0, jumpAirborneHold);
        }
    }

    void EnterJumpLanding()
    {
        if (_jumpPhase == JumpPhase.Landing)
            return;

        _jumpPhase = JumpPhase.Landing;
        _currentSpeed *= landingSpeedRetention;

        if (_jumpLocksUntilAnim)
            SetTravelJumpRootMotion(false);

        if (_animator != null)
            _animator.speed = 1f;

        if (HasAnimState("Landing"))
        {
            float length = PlayAndMeasure("Landing", 0.06f, true);
            _actionUntil = Time.time + Mathf.Max(landingMinDuration, length);
            return;
        }

        if (HasAnimState("Jump"))
        {
            _playingState = "Landing";
            float clipLength = PlaybackLength();
            if (_animator != null && clipLength > 0f)
            {
                _animator.CrossFadeInFixedTime(
                    "Jump",
                    0.08f,
                    0,
                    jumpLandingStart * clipLength);
            }

            float remain = clipLength > 0f
                ? clipLength * (1f - jumpLandingStart)
                : landingMinDuration;
            _actionUntil = Time.time + Mathf.Max(landingMinDuration, remain);
            return;
        }

        PlayState("Idle", 0.12f);
        _actionUntil = Time.time + landingMinDuration;
    }

    void FinishJump()
    {
        _action = ActionKind.None;
        _jumpPhase = JumpPhase.None;
        _jumpLeftGround = false;
        SetTravelJumpRootMotion(false);
        if (_animator != null)
            _animator.speed = 1f;
    }

    // 受击动画播完后结束硬直。
    void UpdateHitReaction()
    {
        if (_action != ActionKind.Hit)
            return;

        if (Time.time < _actionUntil)
            return;
        if (CurrentNormalizedTime() < 1f && Time.time < _actionUntil + 0.45f)
            return;

        FinishHitReaction();
    }

    // 退出受击并抬脚出地面。
    void FinishHitReaction()
    {
        _action = ActionKind.None;
        _verticalVelocity.y = -2f;
        PlayState("Idle", 0.08f, true);
        CharacterBodyFit.LiftFeetOutOfGround(_controller, transform);
    }

    // 读取键盘移动与动作按键。
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

    // 把摇杆输入转成相机平面方向。
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

    // 死亡时只施加重力。
    void ApplyGravityOnly()
    {
        if (_controller.isGrounded && _verticalVelocity.y < 0f)
            _verticalVelocity.y = -2f;
        _verticalVelocity.y += gravity * Time.deltaTime;
        _controller.Move(_verticalVelocity * Time.deltaTime);
    }

    // 在走跑之间切动画并尽量保持相位。
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

        // 走跑互切时对齐步伐相位，跑切走淡入稍长。
        float destLength = PlaybackLength();
        float fade = fromRun && stateName == "Walk" ? 0.28f : 0.2f;
        _animator.CrossFadeInFixedTime(stateName, fade, 0, destLength > 0f ? phase * destLength : 0f);
    }

    // 播放指定动画状态。
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

    // 按当前速度选择跳跃动画。
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

    // 播放动画并返回片段时长。
    float PlayAndMeasure(string stateName, float fade, bool restart)
    {
        PlayState(stateName, fade, restart);
        if (_animator != null)
            _animator.Update(0f);
        return PlaybackLength();
    }

    // 当前动画播放时长。
    float PlaybackLength()
    {
        return AnimPlayback.Length(_animator);
    }

    // 当前动画归一化时间。
    float CurrentNormalizedTime()
    {
        return AnimPlayback.NormalizedTime(_animator);
    }

    // 收集位移跳跃的根运动增量。
    void OnAnimatorMove()
    {
        if (_action == ActionKind.Jump && _jumpLocksUntilAnim && _animator != null)
            _jumpRootDelta = _animator.deltaPosition;
    }

    // 缩放变化时重适配身体，并锁走跑摇摆。
    void LateUpdate()
    {
        if (transform.lossyScale != _fittedLossyScale)
            FitBodyToCurrentScale();

        LockLocomotionSway();
        if (_action == ActionKind.Hit)
            CharacterBodyFit.LiftFeetOutOfGround(_controller, transform);
        UpdatePunchRangeVisual();
    }

    // 开关位移跳跃的根运动。
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

    // 把视觉位移提交到 CharacterController，并拉回髋部水平偏移。
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

    // 更新出拳扇形可视化。
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

    // 按体型缩放后的出拳半径。
    float PunchRadius()
    {
        return punchRange * MotionScale;
    }

    // 锁住走跑/出拳/受击时的左右摇摆。
    void LockLocomotionSway()
    {
        if (_animator == null || !_animator.isHuman)
            return;
        if (_playingState != "Walk" && _playingState != "Run" &&
            _action != ActionKind.Punch && _action != ActionKind.Hit &&
            _jumpPhase != JumpPhase.Landing)
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

        // 髋部锁在角色中轴，受击时限制下沉。
        Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
            return;

        Vector3 local = transform.InverseTransformPoint(hips.position);
        local.x = 0f;
        if (_action == ActionKind.Punch)
            local.z = 0f;
        if (_action == ActionKind.Hit)
        {
            local.z = 0f;
            if (_hitHipLocalY > 0.05f)
                local.y = Mathf.Max(_hitHipLocalY - 0.12f, local.y);
        }
        hips.position = transform.TransformPoint(local);
    }

    /// <summary>按当前缩放适配碰撞体。</summary>
    public void FitBodyToCurrentScale()
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
        CharacterBodyFit.Apply(_controller, transform);
        _fittedLossyScale = transform.lossyScale;
    }

    // 缓存站立时髋部高度，供受击抬脚用。
    void CacheStandingHip()
    {
        _hitHipLocalY = -1f;
        if (_animator == null || !_animator.isHuman)
            return;
        Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
            return;
        _hitHipLocalY = transform.InverseTransformPoint(hips.position).y;
    }

    // 开始指定段的出拳连招。
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
        HitFeel.PunchSwing();
    }

    // 推进连招命中、衔接与收招。
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

        // 本段播完：可衔接则下一段，否则收招。
        if (_comboReady)
        {
            BeginPunch(next);
            return;
        }

        ResetPunchCombo();
        _action = ActionKind.None;
        _actionUntil = 0f;
    }

    // 解析连招段对应的动画名。
    string PunchState(int comboIndex)
    {
        if (comboIndex < 0 || comboIndex >= ComboStates.Length)
            comboIndex = 0;
        if (HasAnimState(ComboStates[comboIndex]))
            return ComboStates[comboIndex];
        return ComboStates[0];
    }

    // 检查动画控制器是否有该状态。
    bool HasAnimState(string stateName)
    {
        return _animator != null && _animator.HasState(0, Animator.StringToHash(stateName));
    }

    // 判断出拳键是否按住。
    bool IsPunchHeld()
    {
        if (_punchButton != null && _punchButton.IsHeld)
            return true;

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.jKey.isPressed;
    }

    // 重置连招状态。
    void ResetPunchCombo()
    {
        _comboIndex = 0;
        _punchConnected = false;
        _punchHitApplied = true;
        _punchQueued = false;
        _comboReady = false;
    }

    // 在扇形范围内对敌人造成拳击伤害。大体积敌人用碰撞体最近点判定。
    void ApplyPunchDamage()
    {
        _punchHitApplied = true;
        float radius = PunchRadius();
        Vector3 origin = transform.position + Vector3.up * (WorldHeight * 0.5f);
        var struck = new HashSet<CharacterCombatAgent>();
        CharacterCombatAgent[] agents = FindObjectsByType<CharacterCombatAgent>(FindObjectsInactive.Exclude);
        for (int i = 0; i < agents.Length; i++)
        {
            CharacterCombatAgent enemy = agents[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if (!IsEnemyInPunchRange(enemy, origin, radius))
                continue;
            struck.Add(enemy);
        }

        _punchConnected = struck.Count > 0;

        // 连招前几段只扣血和受击反馈，最后一击才击退。
        bool finisher = _comboIndex >= ComboStates.Length - 1;
        float knockback = finisher ? HitFeel.PunchKnockback(_comboIndex) : 0f;
        foreach (CharacterCombatAgent enemy in struck)
        {
            enemy.TakeDamage(
                punchDamage,
                transform.position,
                knockback,
                0f,
                HitFeel.PunchDeathFly(_comboIndex));
        }

        if (_punchConnected)
            HitFeel.PunchConnected(
                _comboIndex,
                transform.position,
                transform.forward,
                struck);
    }

    // 用碰撞体最近点判断是否打中，避免大体型敌人原点太远被扇形滤掉。
    bool IsEnemyInPunchRange(CharacterCombatAgent enemy, Vector3 origin, float radius)
    {
        var controller = enemy.GetComponent<CharacterController>();
        if (controller != null && controller.enabled)
        {
            Vector3 closest = controller.ClosestPoint(origin);
            if (InPunchArc(closest, radius))
                return true;
        }

        Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;
            if (InPunchArc(col.ClosestPoint(origin), radius))
                return true;
        }

        return InPunchArc(enemy.transform.position, radius);
    }

    // 判断点是否落在出拳扇形内。
    bool InPunchArc(Vector3 worldPoint, float radius)
    {
        Vector3 to = worldPoint - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance > radius)
            return false;
        if (distance < 0.01f)
            return true;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return true;
        return Vector3.Angle(forward, to) <= PunchArcDegrees * 0.5f;
    }

    // 在编辑器中画出拳扇形。
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
