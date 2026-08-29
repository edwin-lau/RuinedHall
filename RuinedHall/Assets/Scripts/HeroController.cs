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

    CharacterController _controller;
    Animator _animator;
    Transform _cameraTransform;
    VirtualJoystick _joystick;
    Vector2 _moveInput;
    Vector3 _verticalVelocity;
    float _moveHoldTime;
    float _actionUntil;
    string _playingState;
    bool _isDead;
    ActionKind _action;

    enum ActionKind
    {
        None,
        Jump,
        Punch,
        Death
    }

    public bool IsDead => _isDead;

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
        if (_isDead || _action == ActionKind.Jump)
            return;

        _action = ActionKind.Punch;
        _actionUntil = Time.time + ClipLength("Punch", 0.75f);
        PlayState("Punch", 0.05f);
    }

    public void Jump()
    {
        if (_isDead || !_controller.isGrounded || _action == ActionKind.Jump)
            return;

        _action = ActionKind.Jump;
        _actionUntil = Time.time + ClipLength("Jump", 1.04f);
        _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        PlayState("Jump", 0.05f);
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
    }

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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

        if (_action != ActionKind.None && Time.time >= _actionUntil)
            _action = ActionKind.None;

        Vector3 planar = CameraRelativeMove();
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

    void PlayState(string stateName, float fade = 0.12f)
    {
        if (_playingState == stateName)
            return;

        _playingState = stateName;
        if (fade <= 0f)
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
}
