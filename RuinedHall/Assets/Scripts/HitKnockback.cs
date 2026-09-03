using UnityEngine;

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

    public bool IsActive => Time.time < _until && _velocity.sqrMagnitude > 0.05f;

    public static void ApplyTo(Component target, Vector3 hitOrigin, float scale = 1f)
    {
        if (target == null)
            return;

        var knockback = target.GetComponent<HitKnockback>();
        if (knockback == null)
            knockback = target.gameObject.AddComponent<HitKnockback>();
        knockback.Apply(hitOrigin, scale);
    }

    public void Apply(Vector3 hitOrigin, float scale = 1f)
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();

        Vector3 direction = transform.position - hitOrigin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;
        direction.Normalize();

        scale = Mathf.Max(0.1f, scale);
        _velocity = direction * impulse * scale + Vector3.up * lift * scale;
        _until = Time.time + duration;
        enabled = true;
    }

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
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
