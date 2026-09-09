using UnityEngine;

/// <summary>
/// 兔子被玩家看见后逃跑，倒计时结束由老鹰叼走飞离并销毁。
/// </summary>
[RequireComponent(typeof(CharacterActionPlayer))]
public sealed class RabbitEagleHunt : MonoBehaviour
{
    [SerializeField] GameObject eagle;
    [SerializeField] float sightRange = 80f;
    [SerializeField] float fleeSpeed = 6.8f;
    [SerializeField] float grabDelay = 120f;
    [SerializeField] float followHeight = 2.8f;
    [SerializeField] float followDistance = 1.6f;
    [SerializeField] float followSpeed = 18f;
    [SerializeField] float stuckCheckInterval = 0.75f;
    [SerializeField] float stuckSpeedRatio = 0.58f;

    CharacterActionPlayer _rabbitActions;
    CharacterController _rabbitController;
    CharacterActionPlayer _eagleActions;
    HeroController _hero;
    Transform _eagleTransform;
    Vector3 _fleePoint;
    Vector3 _escapeDir = Vector3.forward;
    float _verticalSpeed;
    float _nextFleeRepath;
    float _huntEndTime;
    float _carryUntil;
    float _waterLockUntil;
    float _wingAngle;
    float _stuckSampleTime;
    Vector3 _stuckSamplePos;
    Phase _phase;

    enum Phase
    {
        Idle,
        Flee,
        Carry
    }

    /// <summary>外部注入老鹰物体并解析其动作组件。</summary>
    public void BindEagle(GameObject eagleObject)
    {
        eagle = eagleObject;
        ResolveEagle();
    }

    /// <summary>缓存兔子组件，保证视野半径下限，并解析老鹰。</summary>
    void Awake()
    {
        _rabbitActions = GetComponent<CharacterActionPlayer>();
        _rabbitController = GetComponent<CharacterController>();
        sightRange = Mathf.Max(sightRange, 80f);
        ResolveEagle();
    }

    /// <summary>开局找玩家，老鹰和兔子都播 Idle。</summary>
    void Start()
    {
        _hero = FindAnyObjectByType<HeroController>();
        ResolveEagle();
        if (_eagleTransform != null)
            PlayEagle("Idle", true);
        PlayRabbit("Idle");
    }

    /// <summary>按当前阶段更新待机、逃跑或被叼走。</summary>
    void Update()
    {
        if (_hero == null)
            _hero = FindAnyObjectByType<HeroController>();

        switch (_phase)
        {
            case Phase.Idle:
                UpdateIdle();
                break;
            case Phase.Flee:
                UpdateFlee();
                break;
            case Phase.Carry:
                UpdateCarry();
                break;
        }
    }

    /// <summary>逃跑或被叼走时，在 LateUpdate 里让老鹰跟上兔子。</summary>
    void LateUpdate()
    {
        if (_phase == Phase.Flee || _phase == Phase.Carry)
            UpdateEagleFollow();
    }

    /// <summary>找到老鹰，解开父节点，并关掉根运动。</summary>
    void ResolveEagle()
    {
        if (eagle == null)
            eagle = GameObject.Find("Eagle");
        if (eagle == null)
            return;

        _eagleActions = eagle.GetComponent<CharacterActionPlayer>() ??
                        eagle.GetComponentInChildren<CharacterActionPlayer>(true);
        _eagleTransform = _eagleActions != null ? _eagleActions.transform : eagle.transform;
        if (_eagleTransform == null)
            return;

        _eagleTransform.SetParent(null, true);
        _eagleTransform.gameObject.SetActive(true);
        var animator = _eagleTransform.GetComponent<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;
    }

    /// <summary>待机：兔子原地 Idle，玩家看见后开始狩猎。</summary>
    void UpdateIdle()
    {
        ApplyRabbitMove(Vector3.zero, false);
        PlayRabbit("Idle");
        if (_hero == null || _hero.IsDead)
            return;
        if (PlayerSeesRabbit())
            BeginHunt();
    }

    /// <summary>进入逃跑：记抓取倒计时，并让兔子跑、老鹰飞。</summary>
    void BeginHunt()
    {
        _phase = Phase.Flee;
        _huntEndTime = Time.time + grabDelay;
        _nextFleeRepath = 0f;
        _stuckSampleTime = Time.time;
        _stuckSamplePos = transform.position;
        ChooseFleePoint(false);
        PlayRabbit("Move", true);
        PlayEagle("Fly", true);
    }

    /// <summary>逃跑：到时被叼走，遇水弹开，否则朝逃离点移动。</summary>
    void UpdateFlee()
    {
        if (Time.time >= _huntEndTime)
        {
            BeginCarry();
            return;
        }

        // 踩水时立刻改逃离方向，避免继续往水里冲
        if (WaterProbe.IsInWater(transform.position))
        {
            BounceOffWater(transform.forward);
            ApplyRabbitMove(_escapeDir * fleeSpeed, true);
            PlayRabbit("Move");
            return;
        }

        if (Time.time >= _nextFleeRepath)
            ChooseFleePoint(false);

        Vector3 offset = _fleePoint - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.35f)
        {
            ChooseFleePoint(false);
            offset = _fleePoint - transform.position;
            offset.y = 0f;
        }

        if (offset.sqrMagnitude > 0.0001f)
        {
            Vector3 direction = offset.normalized;
            Face(direction);
            if (!ApplyRabbitMove(direction * fleeSpeed, false))
                BounceOffWater(direction);
            else
                CheckStuck(direction, fleeSpeed);
            PlayRabbit("Move");
        }
        else
        {
            ApplyRabbitMove(Vector3.zero, false);
        }
    }

    /// <summary>实际位移远低于预期时，侧向改道绕开卡住点。</summary>
    void CheckStuck(Vector3 intendedDirection, float speed)
    {
        if (Time.time < _stuckSampleTime + stuckCheckInterval)
            return;

        float elapsed = Time.time - _stuckSampleTime;
        float actual = Vector3.Distance(transform.position, _stuckSamplePos);
        float expected = speed * elapsed;
        _stuckSampleTime = Time.time;
        _stuckSamplePos = transform.position;

        if (expected < 0.5f || actual >= expected * stuckSpeedRatio)
            return;

        Vector3 side = Vector3.Cross(Vector3.up, intendedDirection).normalized;
        if (side.sqrMagnitude < 0.01f)
            side = transform.right;
        _escapeDir = UnityEngine.Random.value > 0.5f ? side : -side;
        ChooseFleePoint(true);
    }

    /// <summary>碰到水后取逃离方向，并短暂锁定该方向。</summary>
    void BounceOffWater(Vector3 incoming)
    {
        _escapeDir = WaterProbe.EscapeDirection(transform.position, incoming);
        if (_escapeDir.sqrMagnitude < 0.01f)
            _escapeDir = -incoming.normalized;
        _waterLockUntil = Time.time + 1.4f;
        ChooseFleePoint(true);
        Face(_escapeDir);
    }

    /// <summary>老鹰飞到兔子斜上方跟随点，并朝目标点转向。</summary>
    void UpdateEagleFollow()
    {
        if (_eagleTransform == null)
        {
            ResolveEagle();
            if (_eagleTransform == null)
                return;
        }

        _wingAngle += 220f * Time.deltaTime;
        Vector3 desired = FollowPoint();
        _eagleTransform.position = Vector3.MoveTowards(
            _eagleTransform.position,
            desired,
            followSpeed * Time.deltaTime);

        Vector3 look = desired - _eagleTransform.position;
        if (look.sqrMagnitude < 0.05f)
            look = transform.forward + Vector3.down * 0.15f;
        if (look.sqrMagnitude > 0.001f)
            _eagleTransform.rotation = Quaternion.Slerp(
                _eagleTransform.rotation,
                Quaternion.LookRotation(look.normalized, Vector3.up),
                10f * Time.deltaTime);
        PlayEagle("Fly");
    }

    /// <summary>算出老鹰相对兔子的侧向跟随点。</summary>
    Vector3 FollowPoint()
    {
        Vector3 lateral = Quaternion.Euler(0f, Mathf.Sin(_wingAngle * Mathf.Deg2Rad) * 18f, 0f) *
                          (-transform.forward);
        return transform.position +
               Vector3.up * followHeight +
               lateral.normalized * followDistance;
    }

    /// <summary>把兔子挂到老鹰脚下，关掉角色控制器。</summary>
    void BeginCarry()
    {
        _phase = Phase.Carry;
        _carryUntil = Time.time + 6.5f;
        if (_rabbitController != null)
            _rabbitController.enabled = false;
        if (_eagleTransform != null)
        {
            transform.SetParent(_eagleTransform, true);
            transform.localPosition = Vector3.down * 0.55f;
        }

        PlayRabbit("Idle", true);
        PlayEagle("Fly", true);
    }

    /// <summary>老鹰带着兔子飞走，时间到后销毁两者。</summary>
    void UpdateCarry()
    {
        if (_eagleTransform != null)
        {
            Vector3 away = Vector3.up * 7.5f + SpawnForward() * 10f;
            _eagleTransform.position += away * Time.deltaTime;
            if (away.sqrMagnitude > 0.001f)
                _eagleTransform.rotation = Quaternion.Slerp(
                    _eagleTransform.rotation,
                    Quaternion.LookRotation(away.normalized, Vector3.up),
                    6f * Time.deltaTime);
        }

        if (Time.time < _carryUntil)
            return;

        if (_eagleTransform != null)
            Destroy(_eagleTransform.gameObject);
        Destroy(gameObject);
    }

    /// <summary>选一个不踩水、不穿水的逃离落点。</summary>
    void ChooseFleePoint(bool fromWater)
    {
        _nextFleeRepath = Time.time + (fromWater ? 1.1f : 0.7f);
        Vector3 origin = transform.position;
        Vector3 heading = fromWater || Time.time < _waterLockUntil
            ? _escapeDir
            : AwayFromHero();
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f)
            heading = transform.forward;
        heading.Normalize();

        // 左右交替扫角度，找一块干燥地面
        Vector3 side = Vector3.Cross(Vector3.up, heading);
        int attempts = fromWater ? 20 : 16;
        for (int i = 0; i < attempts; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            float yaw = fromWater ? (i / 2) * 12f : (i / 2) * 22f;
            Vector3 dir = Quaternion.Euler(0f, yaw * sign, 0f) * heading;
            Vector3 candidate = origin + dir * UnityEngine.Random.Range(fromWater ? 8f : 5f, fromWater ? 14f : 11f);
            Vector3 grounded = SampleGround(candidate);
            if (WaterProbe.IsInWater(grounded) || WaterProbe.CrossesWater(origin, grounded))
                continue;
            _fleePoint = grounded;
            return;
        }

        if (WaterProbe.TryFindLand(origin + heading * 10f, 14f, out Vector3 land) &&
            !WaterProbe.CrossesWater(origin, land))
        {
            _fleePoint = land;
            return;
        }

        _fleePoint = origin + heading * 6f;
        _fleePoint.y = origin.y;
    }

    /// <summary>水平方向上远离玩家；过近则沿自身朝前。</summary>
    Vector3 AwayFromHero()
    {
        if (_hero == null)
            return transform.forward;
        Vector3 away = transform.position - _hero.transform.position;
        away.y = 0f;
        return away.sqrMagnitude < 0.2f ? transform.forward : away.normalized;
    }

    /// <summary>先采样地形高度，失败再向下射线找地面。</summary>
    Vector3 SampleGround(Vector3 xz)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 local = xz - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                continue;
            float y = terrain.SampleHeight(xz) + terrain.transform.position.y;
            return new Vector3(xz.x, y, xz.z);
        }

        if (Physics.Raycast(xz + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 80f))
            return hit.point;
        return xz;
    }

    /// <summary>玩家活着且兔子在主相机视野与距离内。</summary>
    bool PlayerSeesRabbit()
    {
        if (_hero == null || Camera.main == null)
            return false;

        float distance = Vector3.Distance(transform.position, _hero.transform.position);
        if (distance > sightRange)
            return false;

        Vector3 viewport = Camera.main.WorldToViewportPoint(transform.position);
        return viewport.z > 0f &&
               viewport.x > 0f &&
               viewport.x < 1f &&
               viewport.y > 0f &&
               viewport.y < 1f;
    }

    /// <summary>取相机水平朝前，作为老鹰飞离方向。</summary>
    Vector3 SpawnForward()
    {
        if (Camera.main == null)
            return Vector3.forward;
        Vector3 forward = Camera.main.transform.forward;
        forward.y = 0.15f;
        return forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
    }

    /// <summary>把兔子转向指定水平方向。</summary>
    void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            540f * Time.deltaTime);
    }

    /// <summary>带重力移动兔子，并尽量避免走进或更深地踏入水里。</summary>
    bool ApplyRabbitMove(Vector3 planar, bool escapingWater)
    {
        if (_rabbitController == null || !_rabbitController.enabled)
            return false;

        Vector3 next = transform.position + new Vector3(planar.x, 0f, planar.z) * Time.deltaTime * 4f;
        bool nextInWater = WaterProbe.IsInWater(next) ||
                           WaterProbe.CrossesWater(transform.position, next);
        if (nextInWater && !escapingWater)
        {
            planar.x = 0f;
            planar.z = 0f;
            ApplyGravity(ref planar);
            _rabbitController.Move(planar * Time.deltaTime);
            return false;
        }

        // 正在脱困时，若下一步更深地踩水就先刹水平速度
        if (nextInWater && escapingWater)
        {
            float now = PlanarWaterPenalty(transform.position);
            float then = PlanarWaterPenalty(next);
            if (then > now + 0.05f)
            {
                planar.x = 0f;
                planar.z = 0f;
            }
        }

        ApplyGravity(ref planar);
        _rabbitController.Move(planar * Time.deltaTime);
        return true;
    }

    /// <summary>点在水里记 1，否则记 0，用来比较踩水深浅。</summary>
    static float PlanarWaterPenalty(Vector3 point)
    {
        return WaterProbe.IsInWater(point) ? 1f : 0f;
    }

    /// <summary>接地时压住下落速度，否则施加重力。</summary>
    void ApplyGravity(ref Vector3 planar)
    {
        if (_rabbitController.isGrounded && _verticalSpeed < 0f)
            _verticalSpeed = -2f;
        else
            _verticalSpeed += -24f * Time.deltaTime;
        planar.y = _verticalSpeed;
    }

    /// <summary>配置里有该动作才让兔子播放。</summary>
    void PlayRabbit(string actionId, bool restart = false)
    {
        if (_rabbitActions != null &&
            _rabbitActions.Profile != null &&
            _rabbitActions.Profile.TryGet(actionId, out _))
            _rabbitActions.Play(actionId, restart);
    }

    /// <summary>配置里有该动作才让老鹰播放。</summary>
    void PlayEagle(string actionId, bool restart = false)
    {
        if (_eagleActions != null &&
            _eagleActions.Profile != null &&
            _eagleActions.Profile.TryGet(actionId, out _))
            _eagleActions.Play(actionId, restart);
    }
}
