using UnityEngine;

/// <summary>
/// 骷髅飞矢：水平飞行，碰到玩家则造成伤害并短时插在身上。
/// </summary>
public sealed class SkeletonArrow : MonoBehaviour
{
    const float VisualScale = 13.5f;

    CharacterCombatAgent _owner;
    Vector3 _direction;
    float _speed;
    float _range;
    float _traveled;
    float _stickUntil;
    float _flightY;
    int _damage;
    bool _stuck;
    bool _launchedFromHand;

    /// <summary>把手上的箭拆下来当飞矢发射。</summary>
    public static SkeletonArrow LaunchFromHand(
        Transform handArrow,
        Vector3 direction,
        CharacterCombatAgent owner,
        int damage,
        float range,
        float speed,
        float stickDuration)
    {
        if (handArrow == null)
            return null;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = handArrow.forward;
        direction.Normalize();

        handArrow.SetParent(null, true);
        handArrow.rotation = Quaternion.LookRotation(direction, Vector3.up);

        var arrow = handArrow.GetComponent<SkeletonArrow>();
        if (arrow == null)
            arrow = handArrow.gameObject.AddComponent<SkeletonArrow>();
        arrow.Init(owner, direction, damage, range, speed, stickDuration, handArrow.position.y, true);
        return arrow;
    }

    /// <summary>从预制体生成一支出手飞矢。</summary>
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
        arrow.Init(owner, direction, damage, range, speed, stickDuration, origin.y, false);
    }

    /// <summary>记下飞行参数，并补一个触发盒。</summary>
    void Init(
        CharacterCombatAgent owner,
        Vector3 direction,
        int damage,
        float range,
        float speed,
        float stickDuration,
        float flightY,
        bool launchedFromHand)
    {
        _owner = owner;
        _direction = new Vector3(direction.x, 0f, direction.z).normalized;
        _damage = Mathf.Max(1, damage);
        _range = Mathf.Max(2f, range);
        _speed = Mathf.Max(4f, speed);
        _stickUntil = stickDuration;
        _flightY = flightY;
        _launchedFromHand = launchedFromHand;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

        var box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.66f, 0.66f, 4.8f);
        box.center = new Vector3(0f, 0f, 1.05f);
    }

    /// <summary>手射出去的箭销毁后，让骷髅再生成手上的箭。</summary>
    void OnDestroy()
    {
        if (_launchedFromHand && _owner != null && !_owner.IsDead)
            _owner.RestoreHandArrow();
    }

    /// <summary>飞行、碰玩家、超射程销毁；已插入则等到时再删。</summary>
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
                0.54f,
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

            // 碰到自己忽略，碰到别的障碍直接消失
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

    /// <summary>造成伤害并挂到玩家身上一小段时间。</summary>
    void StickTo(HeroController hero, Vector3 worldPoint)
    {
        _stuck = true;
        hero.TakeDamage(_damage, transform.position);
        transform.SetParent(hero.transform, true);
        worldPoint.y = hero.transform.position.y + 0.95f;
        transform.position = worldPoint;
        _stickUntil = Time.time + Mathf.Max(0.2f, _stickUntil);
    }

    /// <summary>触发器碰到玩家同样结算插入。</summary>
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
