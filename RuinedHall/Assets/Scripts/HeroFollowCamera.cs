using UnityEngine;

public class HeroFollowCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float followSmoothing = 14f;

    Quaternion _lockedRotation;
    Vector3 _offset;
    bool _hasView;
    bool _snapNow;

    public void Bind(Transform followTarget, Vector3 worldOffset, Quaternion worldRotation)
    {
        target = followTarget;
        _offset = worldOffset;
        _lockedRotation = worldRotation;
        _hasView = target != null;
        _snapNow = true;
        Apply(1f);
    }

    void LateUpdate()
    {
        if (target == null || !_hasView)
            return;

        float speed = 0f;
        var hero = target.GetComponent<HeroController>();
        if (hero != null)
            speed = hero.PlanarSpeed;

        // When the hero brakes, catch up immediately so the character does not slide backward in frame.
        float t = _snapNow || followSmoothing <= 0f || speed <= 0.35f
            ? 1f
            : 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        _snapNow = false;
        Apply(t);
    }

    void Apply(float t)
    {
        Vector3 desired = target.position + _offset;
        transform.position = t >= 1f
            ? desired
            : Vector3.Lerp(transform.position, desired, t);
        transform.rotation = _lockedRotation;
    }
}
