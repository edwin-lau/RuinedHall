using UnityEngine;

/// <summary>
/// 受击击退：给速度并衰减。玩家由 HeroController 合并进一次 Move，NPC 自己在 LateUpdate 里推。
/// </summary>
[DisallowMultipleComponent]
public sealed class HitKnockback : MonoBehaviour
{
    [SerializeField] float impulse = 11.5f;
    [SerializeField] float duration = 0.18f;
    [SerializeField] float lift = 2.2f;
    [SerializeField] float damping = 12f;

    CharacterController _controller;
    Vector3 _velocity;
    float _until;

    /// <summary>击退是否还在生效。</summary>
    public bool IsActive => Time.time < _until && _velocity.sqrMagnitude > 0.05f;

    /// <summary>当前击退速度；未激活时为零。玩家移动会读这个值。</summary>
    public Vector3 Velocity => IsActive ? _velocity : Vector3.zero;

    /// <summary>按帧衰减击退速度，不负责真正位移（玩家路径）。</summary>
    public void Tick(float deltaTime)
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
        if (Time.time >= _until || _velocity.sqrMagnitude < 0.05f)
        {
            _velocity = Vector3.zero;
            return;
        }

        _velocity *= Mathf.Exp(-damping * deltaTime);
    }

    /// <summary>给目标加上本组件并立刻施加击退。liftScale=0 时不抬离地面。</summary>
    public static void ApplyTo(Component target, Vector3 hitOrigin, float scale = 1f, float liftScale = 1f)
    {
        if (target == null)
            return;

        var knockback = target.GetComponent<HitKnockback>();
        if (knockback == null)
            knockback = target.gameObject.AddComponent<HitKnockback>();
        knockback.Apply(hitOrigin, scale, liftScale);
    }

    /// <summary>从受击点算水平方向，叠冲量与可选上抬。</summary>
    public void Apply(Vector3 hitOrigin, float scale = 1f, float liftScale = 1f)
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();

        // 水平离开攻击者；重合时用自身背后方向
        Vector3 direction = transform.position - hitOrigin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;
        direction.Normalize();

        scale = Mathf.Max(0.1f, scale);
        _velocity = direction * impulse * scale + Vector3.up * lift * scale * Mathf.Max(0f, liftScale);
        _until = Time.time + duration;
        enabled = true;
    }

    /// <summary>立刻停下击退，用于连招前几段只受击不后退。</summary>
    public void Stop()
    {
        _velocity = Vector3.zero;
        _until = 0f;
    }

    /// <summary>缓存 CharacterController。</summary>
    void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    /// <summary>NPC 自己把击退写进 Move；玩家交给 HeroController，避免两次 Move 穿地。</summary>
    void LateUpdate()
    {
        if (GetComponent<HeroController>() != null)
            return;

        if (_controller == null || !_controller.enabled || !isActiveAndEnabled)
            return;

        if (Time.time >= _until || _velocity.sqrMagnitude < 0.05f)
        {
            _velocity = Vector3.zero;
            return;
        }

        _controller.Move(_velocity * Time.deltaTime);
        _velocity *= Mathf.Exp(-damping * Time.deltaTime);
    }
}
