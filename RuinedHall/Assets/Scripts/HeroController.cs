using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class HeroController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 6.4f;
    [SerializeField] float runSpeed = 14.8f;
    [SerializeField] float rotateSpeed = 720f;
    [SerializeField] float jumpHeight = 1.65f;
    [SerializeField] float gravity = -32f;
    [SerializeField] float walkToRunDelay = 1f;
    [SerializeField] float moveDeadZone = 0.18f;
    [SerializeField] int maxHealth = 100;
    [SerializeField] int punchDamage = 20;
    [SerializeField] float punchRange = 1.4f;

    CharacterController _controller;
    Animator _animator;
    Transform _cameraTransform;
    VirtualJoystick _joystick;
    HitKnockback _knockback;
    Vector2 _moveInput;
    Vector3 _verticalVelocity;
    float _moveHoldTime;
    float _actionUntil;
    float _punchHitAt;
    string _playingState;
    int _currentHealth;
    bool _punchHitApplied;
    bool _isDead;
    bool _jumpLeftGround;
    ActionKind _action;

    enum ActionKind
    {
        None,
        Jump,
        Punch,
        Death
    }

    public bool IsDead => _isDead;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public event Action<int, int> HealthChanged;
    public event Action Died;

    public void SetJoystick(VirtualJoystick joystick)
    {
        _joystick = joystick;
    }

    public void SetMoveInput(Vector2 input)
    {
        _moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void Punch()
    {
        if (_isDead || _action == ActionKind.Punch)
            return;
        if (_action == ActionKind.Jump && !_controller.isGrounded)
            return;

        float clipLength = ClipLength("Punch", 0.55f);
        _action = ActionKind.Punch;
        _actionUntil = Time.time + clipLength;
        _punchHitAt = Time.time + clipLength * 0.38f;
        _punchHitApplied = false;
        PlayState("Punch", 0.04f, true);
    }

    public void Jump()
    {
        if (_isDead || !_controller.isGrounded || _action == ActionKind.Jump)
            return;

        _action = ActionKind.Jump;
        _actionUntil = Time.time + 0.18f;
        _jumpLeftGround = false;
        _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        PlayState("Jump", 0.04f, true);
    }

    public void Die()
    {
        if (_isDead)
            return;

        _isDead = true;
        _action = ActionKind.Death;
        _moveInput = Vector2.zero;
        _verticalVelocity = Vector3.zero;
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
        _punchHitApplied = true;
        _jumpLeftGround = false;
        _moveInput = Vector2.zero;
        _verticalVelocity = Vector3.zero;
        _currentHealth = maxHealth;
        NotifyHealth();
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
            Die();
    }

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _knockback = GetComponent<HitKnockback>();
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _currentHealth = maxHealth;
        NotifyHealth();
    }

    void NotifyHealth()
    {
        HealthChanged?.Invoke(_currentHealth, maxHealth);
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

        if (_action == ActionKind.Punch &&
            !_punchHitApplied &&
            Time.time >= _punchHitAt)
        {
            ApplyPunchDamage();
        }

        if (_action == ActionKind.Jump)
        {
            if (!_controller.isGrounded)
                _jumpLeftGround = true;
            else if (_jumpLeftGround && _verticalVelocity.y <= 0f)
                _action = ActionKind.None;
        }

        if (_action != ActionKind.None && Time.time >= _actionUntil && _action != ActionKind.Jump)
            _action = ActionKind.None;

        if (_action == ActionKind.Jump && Time.time >= _actionUntil && _controller.isGrounded)
            _action = ActionKind.None;

        Vector3 planar = CameraRelativeMove();
        if (_knockback == null)
            _knockback = GetComponent<HitKnockback>();
        if (_knockback != null && _knockback.IsActive)
            planar *= 0.12f;
        bool moving = planar.sqrMagnitude >= moveDeadZone * moveDeadZone;

        if (moving)
        {
            _moveHoldTime += Time.deltaTime;
            Quaternion look = Quaternion.LookRotation(planar.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, rotateSpeed * Time.deltaTime);
        }
        else
        {
            _moveHoldTime = 0f;
        }

        bool running = moving && _moveHoldTime >= walkToRunDelay;
        float speed = running ? runSpeed : walkSpeed;
        Vector3 motion = moving ? planar.normalized * speed : Vector3.zero;

        if (_controller.isGrounded && _verticalVelocity.y < 0f)
            _verticalVelocity.y = -2f;
        _verticalVelocity.y += gravity * Time.deltaTime;
        motion += _verticalVelocity;

        _controller.Move(motion * Time.deltaTime);

        if (_action == ActionKind.None)
        {
            if (!moving)
                PlayState("Idle");
            else if (running)
                PlayState("Run");
            else
                PlayState("Walk");
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

    void PlayState(string stateName, float fade = 0.12f, bool restart = false)
    {
        if (!restart && _playingState == stateName)
            return;

        _playingState = stateName;
        if (restart || fade <= 0f)
            _animator.Play(stateName, 0, 0f);
        else
            _animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
    }

    float ClipLength(string stateName, float fallback)
    {
        if (_animator.runtimeAnimatorController == null)
            return fallback;

        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == stateName)
                return Mathf.Max(0.05f, clip.length);
        }

        return fallback;
    }

    void ApplyPunchDamage()
    {
        _punchHitApplied = true;
        Vector3 center = transform.position + Vector3.up * 0.9f + transform.forward * 0.75f;
        Collider[] hits = Physics.OverlapSphere(
            center,
            punchRange,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        CharacterCombatAgent closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (Collider hit in hits)
        {
            CharacterCombatAgent enemy = hit.GetComponentInParent<CharacterCombatAgent>();
            if (enemy == null || enemy.IsDead)
                continue;

            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closest = enemy;
            closestDistance = distance;
        }

        if (closest != null)
            closest.TakeDamage(punchDamage, transform.position);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position + Vector3.up * 0.9f + transform.forward * 0.75f;
        Gizmos.DrawWireSphere(center, punchRange);
    }
}
