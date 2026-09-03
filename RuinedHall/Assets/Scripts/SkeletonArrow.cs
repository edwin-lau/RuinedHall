using UnityEngine;

public sealed class SkeletonArrow : MonoBehaviour
{
    const float VisualScale = 4.5f;

    CharacterCombatAgent _owner;
    Vector3 _direction;
    float _speed;
    float _range;
    float _traveled;
    float _stickUntil;
    float _flightY;
    int _damage;
    bool _stuck;

    public static void Launch(
        GameObject prefab,
        Vector3 origin,
        Vector3 direction,
        CharacterCombatAgent owner,
        int damage,
        float range,
        float speed,
        float stickDuration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;
        direction.Normalize();

        var root = new GameObject("Skeleton_Arrow");
        root.transform.position = origin;
        root.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        GameObject visual = Object.Instantiate(prefab, root.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * VisualScale;

        var arrow = root.AddComponent<SkeletonArrow>();
        arrow.Init(owner, direction, damage, range, speed, stickDuration);
    }

    void Init(
        CharacterCombatAgent owner,
        Vector3 direction,
        int damage,
        float range,
        float speed,
        float stickDuration)
    {
        _owner = owner;
        _direction = new Vector3(direction.x, 0f, direction.z).normalized;
        _damage = Mathf.Max(1, damage);
        _range = Mathf.Max(2f, range);
        _speed = Mathf.Max(4f, speed);
        _stickUntil = stickDuration;
        _flightY = transform.position.y;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.22f, 0.22f, 1.6f);
        box.center = new Vector3(0f, 0f, 0.35f);
    }

    void Update()
    {
        if (_stuck)
        {
            if (Time.time >= _stickUntil)
                Destroy(gameObject);
            return;
        }

        float step = _speed * Time.deltaTime;
        Vector3 next = transform.position + _direction * step;
        next.y = _flightY;
        if (Physics.SphereCast(
                transform.position,
                0.18f,
                _direction,
                out RaycastHit hit,
                step + 0.2f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore))
        {
            var hero = hit.collider.GetComponentInParent<HeroController>();
            if (hero != null && !hero.IsDead)
            {
                StickTo(hero, hit.point);
                return;
            }

            if (_owner != null && hit.collider.GetComponentInParent<CharacterCombatAgent>() == _owner)
            {
                transform.position = next;
                _traveled += step;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            transform.position = next;
            _traveled += step;
        }

        if (_traveled >= _range)
            Destroy(gameObject);
    }

    void StickTo(HeroController hero, Vector3 worldPoint)
    {
        _stuck = true;
        hero.TakeDamage(_damage, transform.position);
        transform.SetParent(hero.transform, true);
        worldPoint.y = hero.transform.position.y + 0.95f;
        transform.position = worldPoint;
        _stickUntil = Time.time + Mathf.Max(0.2f, _stickUntil);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_stuck || other == null)
            return;
        var hero = other.GetComponentInParent<HeroController>();
        if (hero == null || hero.IsDead)
            return;
        StickTo(hero, transform.position);
    }
}
