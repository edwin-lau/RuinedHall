using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 敌人战斗代理：巡逻、追击、近战/远程攻击、受击、睡眠埋伏与死亡爆碎。
/// </summary>
[RequireComponent(typeof(CharacterActionPlayer))]
[RequireComponent(typeof(CharacterController))]
public sealed class CharacterCombatAgent : MonoBehaviour
{
    [Header("Semantic Actions")]
    [SerializeField] string idleAction = "Idle";
    [SerializeField] string moveAction = "Move";
    [SerializeField] string hitAction = "Hit";
    [SerializeField] string deathAction = "Death";
    [SerializeField] string[] attackActions = { "Attack" };
    [SerializeField] string[] idleVariations = System.Array.Empty<string>();

    [Header("Detection")]
    [SerializeField] float detectionRange = 12f;
    [SerializeField] float loseInterestRange = 20f;
    [SerializeField] float attackRange = 1.5f;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 2.5f;
    [SerializeField] float rotationSpeed = 420f;
    [SerializeField] float gravity = -24f;

    [Header("Combat")]
    [SerializeField] int maxHealth = 40;
    [SerializeField] int attackDamage = 15;
    [SerializeField] float attackCooldown = 1.2f;

    [Header("Idle")]
    [SerializeField] Vector2 idleVariationInterval = new Vector2(4f, 8f);

    [Header("Patrol")]
    [SerializeField] bool patrol = true;
    [SerializeField] float patrolRadius = 14f;
    [SerializeField] float patrolArriveDistance = 0.85f;
    [SerializeField] float patrolSpeedMultiplier = 0.62f;
    [SerializeField] Vector2 patrolWait = new Vector2(1.4f, 3.8f);

    [Header("Death")]
    [SerializeField] float corpseHoldDuration = 1.2f;
    [SerializeField] float fadeDuration = 2.5f;

    [Header("Ambush")]
    [SerializeField] bool passiveUntilHit;
    [SerializeField] bool chargeOnAttack;
    [SerializeField] bool spawnFromGround;
    [SerializeField] string spawnAction = "Spawn";
    [SerializeField] float spawnTriggerRange = 16f;
    [SerializeField] string throwAction = "Throw";
    [SerializeField] string projectileResource;
    [SerializeField] float projectileRange = 18f;
    [SerializeField] float projectileSpeed = 24f;
    [SerializeField] float projectileStickDuration = 1f;
    [SerializeField] bool sleepUntilHit;
    [SerializeField] string sleepAction = "Sleep";
    [SerializeField] string wakeAction = "Wake";
    [SerializeField] string fallAsleepAction = "FallAsleep";
    [SerializeField] int retreatAfterPlayerHits;

    [Header("Rewards")]
    [SerializeField] bool elite;
    [SerializeField] string eliteTitle;
    [SerializeField] int goldReward = 3;
    [SerializeField] bool trainingDummy;

    CharacterActionPlayer _actions;
    CharacterController _controller;
    HeroController _target;
    HitKnockback _knockback;
    int _health;
    float _verticalSpeed;
    float _nextAttackTime;
    float _attackHitNormalized;
    float _nextIdleVariationTime;
    bool _attackApplied;
    bool _attacking;
    bool _hitReacting;
    bool _chasing;
    bool _idleVariationPlaying;
    bool _paused;
    bool _dead;
    bool _patrolling;
    bool _sleeping;
    bool _waking;
    bool _retreating;
    bool _fallingAsleep;
    bool _combatEngaged;
    float _hiddenFromViewSince = -1f;
    Vector3 _homePosition;
    Vector3 _patrolPoint;
    float _waitUntil;
    float _patrolGiveUpTime;
    float _stuckTimer;
    float _lastProgressDistance;
    int _playerHitsLanded;
    WorldHealthBar _healthBar;
    float _animSpeed = 1f;
    bool _waitingToSpawn;
    bool _spawning;
    PunchRangeDisplay _attackRange;
    float _dummyPlantedY;
    bool _dummyPlanted;
    int _dummyPlantFrames;
    Renderer[] _visualRenderers;
    Transform _handArrow;
    Transform _handArrowParent;
    Vector3 _handArrowLocalPos = new Vector3(0.871f, 1.234f, -0.023f);
    Quaternion _handArrowLocalRot = Quaternion.Euler(-181.01f, 160.437f, 0f);
    Vector3 _handArrowLocalScale = Vector3.one * 1.619f;
    EnemySpawner _perfSpawner;
    int _perfCampIndex = -1;
    bool _campDormant;
    AgentLodTier _lodTier = AgentLodTier.Near;
    int _lodPhase;
    Vector3 _lastHitOrigin;
    float _deathFlyScale = 1f;

    /// <summary>按与玩家距离划分的 AI 更新档位。</summary>
    public enum AgentLodTier
    {
        Near,
        Mid,
        Far
    }

    /// <summary>是否已死亡。</summary>
    public bool IsDead => _dead;
    /// <summary>是否被外部暂停。</summary>
    public bool IsPaused => _paused;
    /// <summary>是否处于追击、攻击或撤退。</summary>
    public bool IsInCombat => _chasing || _attacking || _retreating;
    /// <summary>是否为精英。</summary>
    public bool IsElite => elite;
    /// <summary>是否为训练木桩。</summary>
    public bool IsTrainingDummy => trainingDummy;
    /// <summary>精英称号。</summary>
    public string EliteTitle => eliteTitle;
    /// <summary>击杀掉落金币。</summary>
    public int GoldReward => goldReward;
    /// <summary>当前生命值。</summary>
    public int CurrentHealth => _health;
    /// <summary>最大生命值。</summary>
    public int MaxHealth => maxHealth;
    /// <summary>动作播放器。</summary>
    public CharacterActionPlayer Actions => _actions;
    public event Action<int, int> HealthChanged;
    public event Action Died;

    // 取组件、套身份并初始化生命值。
    void Awake()
    {
        _actions = GetComponent<CharacterActionPlayer>();
        _controller = GetComponent<CharacterController>();
        _knockback = GetComponent<HitKnockback>();
        EnemyIdentity.Apply(this);
        _health = maxHealth;
        NotifyHealth();
    }

    /// <summary>写入精英标记、称号与金币奖励。</summary>
    public void ApplyRewards(bool isElite, string title, int gold)
    {
        elite = isElite;
        eliteTitle = title ?? "";
        goldReward = Mathf.Max(0, gold);
    }

    /// <summary>写入生命、伤害、射程、冷却、移速与动画倍速。</summary>
    public void ApplyCombatTuning(
        int hp,
        int damage,
        float range,
        float cooldown,
        float speed,
        float animSpeed)
    {
        maxHealth = Mathf.Max(1, hp);
        attackDamage = Mathf.Max(1, damage);
        attackRange = Mathf.Max(0.4f, range);
        attackCooldown = Mathf.Max(0.08f, cooldown);
        moveSpeed = Mathf.Max(0.4f, speed);
        _animSpeed = Mathf.Max(0.2f, animSpeed);
        if (_actions != null)
            _actions.SetPlaybackSpeed(_animSpeed);
    }

    /// <summary>写入待机变体、间隔与巡逻半径。</summary>
    public void ApplyIdleHabits(string[] variations, Vector2 interval, float radius)
    {
        idleVariations = variations ?? System.Array.Empty<string>();
        idleVariationInterval = interval;
        patrol = true;
        patrolRadius = Mathf.Max(2f, radius);
    }

    /// <summary>被打中前是否保持被动。</summary>
    public void ApplyPassiveUntilHit(bool value)
    {
        passiveUntilHit = value;
    }

    /// <summary>配置为训练木桩：高血、不巡逻、不反击。</summary>
    public void ApplyTrainingDummy(string title = "肉桩")
    {
        trainingDummy = true;
        elite = true;
        eliteTitle = title ?? "肉桩";
        goldReward = 0;
        patrol = false;
        passiveUntilHit = true;
        chargeOnAttack = false;
        sleepUntilHit = false;
        spawnFromGround = false;
        maxHealth = 200;
        _health = maxHealth;
        NotifyHealth();
    }

    /// <summary>攻击时是否继续冲锋。</summary>
    public void ApplyChargeOnAttack(bool value)
    {
        chargeOnAttack = value;
    }

    /// <summary>立刻进入追击并锁定英雄。</summary>
    public void ForceChase()
    {
        _chasing = true;
        _combatEngaged = true;
        _patrolling = false;
        _idleVariationPlaying = false;
        AcquireTarget();
    }

    /// <summary>覆盖近战攻击动作列表。</summary>
    public void ApplyAttackActions(params string[] actions)
    {
        attackActions = actions ?? System.Array.Empty<string>();
    }

    /// <summary>配置破土现身，玩家靠近才钻出。</summary>
    public void ApplySpawnFromGround(float triggerRange)
    {
        spawnFromGround = true;
        spawnAction = "Spawn";
        spawnTriggerRange = Mathf.Max(4f, triggerRange);
    }

    /// <summary>配置远程投掷弹药与飞行参数。</summary>
    public void ApplyRangedThrow(string resource, float flightRange, float speed)
    {
        throwAction = "Throw";
        projectileResource = resource ?? "";
        projectileRange = Mathf.Max(4f, flightRange);
        projectileSpeed = Mathf.Max(4f, speed);
        projectileStickDuration = 1f;
    }

    // 是否具备可用的远程投掷动作与弹药。
    bool HasRangedThrow =>
        !string.IsNullOrWhiteSpace(projectileResource) &&
        (_actions == null || _actions.TryGetAction(throwAction, out _));

    // 广播当前生命值。
    void NotifyHealth()
    {
        HealthChanged?.Invoke(_health, maxHealth);
    }

    // 锁定家园点，并按埋伏类型进入待机、睡眠或破土等待。
    void Start()
    {
        AcquireTarget();
        _homePosition = transform.position;
        _waitUntil = Time.time + UnityEngine.Random.Range(0.4f, 2.4f);
        if (spawnFromGround)
            BeginWaitToSpawn();
        else if (sleepUntilHit)
            EnterSleep();
        else
        {
            PlayIfAvailable(idleAction, true);
            ScheduleIdleVariation();
        }

        if (trainingDummy)
            PlantDummy();

        // 睡眠/破土怪先藏血条；远程怪缓存手上箭。
        _healthBar = WorldHealthBar.Attach(this);
        if ((sleepUntilHit || spawnFromGround) && _healthBar != null)
            _healthBar.Hide();
        if (HasRangedThrow)
            CacheHandArrow();
        NotifyHealth();
    }

    // 木桩前几帧反复贴地，并更新精英射程圈。
    void LateUpdate()
    {
        if (trainingDummy && _dummyPlantFrames < 24)
        {
            PlantDummy();
            _dummyPlantFrames++;
        }

        UpdateEliteRangeVisual();
    }

    // 精英显示近战/远程双圈范围。
    void UpdateEliteRangeVisual()
    {
        if (!elite || trainingDummy || _dead || _waitingToSpawn || _sleeping)
        {
            if (_attackRange != null)
                _attackRange.Hide();
            return;
        }

        if (_attackRange == null)
            _attackRange = PunchRangeDisplay.Create();

        float inner = attackRange;
        float outer = HasRangedThrow ? projectileRange : attackRange * 1.35f;
        _attackRange.ShowRings(
            transform.position,
            inner,
            outer,
            _attacking,
            _attackApplied,
            "出手",
            HasRangedThrow ? "射程" : "打中");
    }

    // 销毁射程可视化。
    void OnDestroy()
    {
        if (_attackRange != null)
            Destroy(_attackRange.gameObject);
    }

    /// <summary>把箭插回手上（投掷后补箭）。</summary>
    public void RestoreHandArrow()
    {
        if (_dead || _handArrow != null || string.IsNullOrWhiteSpace(projectileResource))
            return;

        GameObject prefab = Resources.Load<GameObject>(projectileResource);
        if (prefab == null)
            return;

        Transform parent = _handArrowParent != null ? _handArrowParent : transform;
        GameObject arrow = Instantiate(prefab, parent);
        arrow.name = "Skeleton_Arrow";
        Transform t = arrow.transform;
        t.localPosition = _handArrowLocalPos;
        t.localRotation = _handArrowLocalRot;
        t.localScale = _handArrowLocalScale;
        _handArrow = t;
    }

    // 缓存手上箭的局部姿态，供投掷后还原。
    void CacheHandArrow()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.Equals("Skeleton_Arrow", System.StringComparison.OrdinalIgnoreCase))
                continue;

            _handArrow = child;
            _handArrowParent = child.parent;
            _handArrowLocalPos = child.localPosition;
            _handArrowLocalRot = child.localRotation;
            _handArrowLocalScale = child.localScale;
            return;
        }
    }

    // 主 AI：LOD、受击、埋伏、追击、攻击与巡逻。
    void Update()
    {
        if (_dead || _paused)
            return;

        // 远档或休眠营地只维持睡眠，中档隔帧更新。
        if (UsesPerformanceLod() && _campDormant)
        {
            MaintainPerformanceSleep();
            return;
        }

        if (UsesPerformanceLod() &&
            !NeedsFullPerformanceAi() &&
            _lodTier == AgentLodTier.Far)
        {
            MaintainPerformanceSleep();
            return;
        }

        if (UsesPerformanceLod() &&
            !NeedsFullPerformanceAi() &&
            _lodTier == AgentLodTier.Mid &&
            (Time.frameCount + _lodPhase) % 3 != 0)
        {
            return;
        }

        if (_knockback == null)
            _knockback = GetComponent<HitKnockback>();
        if (_knockback != null && _knockback.IsActive)
        {
            ApplyMovement(Vector3.zero);
            if (_hitReacting && !ActionFinished())
                return;
        }

        if (_target == null)
            AcquireTarget();

        // 破土现身：等玩家靠近或播完钻出动画。
        if (_waitingToSpawn)
        {
            UpdateWaitToSpawn();
            return;
        }

        if (_spawning)
        {
            ApplyMovement(Vector3.zero);
            if (ActionFinished())
                FinishGroundSpawn();
            return;
        }

        if (_hitReacting)
        {
            if (trainingDummy)
                KeepDummyPlanted();
            else
                ApplyMovement(Vector3.zero);
            if (!ActionFinished())
                return;
            _hitReacting = false;
        }

        // 木桩只贴地挨打，不跑 AI。
        if (trainingDummy)
        {
            KeepDummyPlanted();
            if (!_hitReacting)
                PlayIfAvailable(idleAction);
            return;
        }

        if (UsesPerformanceLod() &&
            !NeedsFullPerformanceAi() &&
            _lodTier == AgentLodTier.Mid)
        {
            UpdateMidTierAi();
            return;
        }

        // 睡眠 / 醒来 / 入睡动画期间站住。
        if (_sleeping)
        {
            ApplyMovement(Vector3.zero);
            PlayIfAvailable(sleepAction);
            return;
        }

        if (_waking)
        {
            ApplyMovement(Vector3.zero);
            if (ActionFinished())
            {
                _waking = false;
                _chasing = true;
                if (_healthBar != null)
                    _healthBar.Show();
            }
            return;
        }

        if (_fallingAsleep)
        {
            if (_combatEngaged &&
                IsVisibleInPlayerView() &&
                _target != null &&
                !_target.IsDead)
            {
                _fallingAsleep = false;
                _chasing = true;
                _hiddenFromViewSince = -1f;
                return;
            }

            ApplyMovement(Vector3.zero);
            if (ActionFinished())
                EnterSleep();
            return;
        }

        if (_retreating)
        {
            UpdateRetreat();
            return;
        }

        // 被动怪没被打醒前只巡逻。
        if (passiveUntilHit && !_chasing)
        {
            UpdatePatrolOrIdle();
            return;
        }

        if (_target == null || _target.IsDead)
        {
            _chasing = false;
            _attacking = false;
            if (ShouldReturnToSleep())
                BeginRetreat();
            else
                UpdatePatrolOrIdle();
            return;
        }

        Vector3 offset = _target.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        // 攻击中：面向目标，到命中帧结算近战或放箭。
        if (_attacking)
        {
            Face(offset);
            if (chargeOnAttack && offset.sqrMagnitude > 0.01f)
                ApplyMovement(offset.normalized * moveSpeed);
            else
                ApplyMovement(Vector3.zero);
            if (!_attackApplied && _actions.NormalizedTime >= _attackHitNormalized)
            {
                _attackApplied = true;
                if (HasRangedThrow)
                    LaunchProjectile();
                else if (distance <= attackRange * 1.35f)
                    ApplyMeleeHit(distance);
            }

            if (ActionFinished())
                _attacking = false;
            return;
        }

        if (distance <= attackRange && Time.time >= _nextAttackTime)
        {
            BeginAttack();
            return;
        }

        // 进警戒圈开始追；已交战且仍在玩家视野内则不脱战去睡觉。
        bool keepFighting = sleepUntilHit && _combatEngaged && IsVisibleInPlayerView();
        _chasing = keepFighting ||
            distance <= detectionRange ||
            (_chasing && distance <= loseInterestRange);
        if (_chasing)
            _combatEngaged = true;
        if (!_chasing || distance < 0.01f)
        {
            if (ShouldReturnToSleep())
                BeginRetreat();
            else
                UpdatePatrolOrIdle();
            return;
        }

        _idleVariationPlaying = false;
        _patrolling = false;
        Vector3 direction = offset / distance;
        Face(direction);
        ApplyMovement(direction * moveSpeed);
        PlayIfAvailable(moveAction);
    }

    /// <summary>从身前默认来源扣血。</summary>
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position + transform.forward);
    }

    /// <summary>从指定来源扣血。</summary>
    public void TakeDamage(int amount, Vector3 hitOrigin)
    {
        TakeDamage(amount, hitOrigin, 1f, 1f);
    }

    /// <summary>扣血、击退并进入受击；木桩不致死，睡眠怪被打会醒来。</summary>
    public void TakeDamage(int amount, Vector3 hitOrigin, float knockbackScale, float liftScale)
    {
        TakeDamage(amount, hitOrigin, knockbackScale, liftScale, 1f);
    }

    /// <summary>扣血；deathFlyScale 只影响普通怪死亡击飞，1 为 Punch4 距离。</summary>
    public void TakeDamage(
        int amount,
        Vector3 hitOrigin,
        float knockbackScale,
        float liftScale,
        float deathFlyScale)
    {
        if (_dead || amount <= 0)
            return;

        if (UsesPerformanceLod())
            ForceWakeFromPerformanceLod();

        if (_waitingToSpawn)
            BeginGroundSpawn();

        // 木桩只扣血弹字，血空后自动回满。
        if (trainingDummy)
        {
            _health = Mathf.Max(0, _health - amount);
            NotifyHealth();
            DamagePopup.Spawn(HeadPoint(), amount);
            _chasing = false;
            _attacking = false;
            _patrolling = false;
            if (_health <= 0)
                StartCoroutine(RefillDummyHealth());
            if (_actions.TryGetAction(hitAction, out _))
            {
                _actions.Play(hitAction, true);
                _hitReacting = true;
            }
            return;
        }

        _health = Mathf.Max(0, _health - amount);
        NotifyHealth();
        _patrolling = false;
        if (_health == 0)
        {
            _lastHitOrigin = hitOrigin;
            _deathFlyScale = Mathf.Max(0.05f, deathFlyScale);
            StartCoroutine(DieAndBurst());
            return;
        }

        if (knockbackScale > 0.001f)
        {
            HitKnockback.ApplyTo(
                this,
                hitOrigin,
                knockbackScale,
                Mathf.Max(0f, liftScale));
            _knockback = GetComponent<HitKnockback>();
        }
        else if (_knockback != null)
        {
            _knockback.Stop();
        }

        // 睡眠怪被打会醒来；其它则进入受击并开始追击。
        if (sleepUntilHit && (_sleeping || _fallingAsleep))
        {
            BeginWake();
            return;
        }

        _chasing = true;
        _combatEngaged = true;

        _attacking = false;
        if (_actions.TryGetAction(hitAction, out _))
        {
            _actions.Play(hitAction, true);
            _hitReacting = true;
        }
    }

    /// <summary>瞬移到世界坐标并重置巡逻家。</summary>
    public void RelocateTo(Vector3 worldPosition)
    {
        if (_dead)
            return;

        if (_controller != null && _controller.enabled)
        {
            _controller.enabled = false;
            transform.position = worldPosition;
            _controller.enabled = true;
        }
        else
        {
            transform.position = worldPosition;
        }

        _homePosition = worldPosition;
        _patrolPoint = worldPosition;
        _patrolling = false;
        _chasing = false;
        _attacking = false;
        _idleVariationPlaying = false;
        _waitUntil = Time.time + UnityEngine.Random.Range(0.2f, 1.1f);
        _verticalSpeed = -2f;
    }

    /// <summary>暂停 AI 与动作播放。</summary>
    public void PauseCharacter()
    {
        _paused = true;
        _actions.Pause();
    }

    /// <summary>恢复 AI 与动作播放。</summary>
    public void ResumeCharacter()
    {
        _paused = false;
        _actions.Resume();
    }

    /// <summary>绑定性能营地，默认先进入远档休眠。</summary>
    public void BindPerformanceCamp(EnemySpawner spawner, int campIndex, int lodPhase)
    {
        _perfSpawner = spawner;
        _perfCampIndex = campIndex;
        _campDormant = true;
        _lodTier = AgentLodTier.Far;
        _lodPhase = ((lodPhase % 3) + 3) % 3;
        EnterPerformanceSleep();
    }

    /// <summary>切换营地休眠：暂停动作与血条。</summary>
    public void SetCampDormant(bool dormant)
    {
        if (trainingDummy || elite || _perfCampIndex < 0)
            return;
        if (_campDormant == dormant)
            return;
        if (dormant && NeedsFullPerformanceAi())
            return;

        _campDormant = dormant;
        if (dormant)
            EnterPerformanceSleep();
        else
            ExitPerformanceSleep();
    }

    /// <summary>设置 LOD 档位；远档休眠，从远档回来则唤醒。</summary>
    public void SetLodTier(AgentLodTier tier)
    {
        if (trainingDummy || elite || _perfCampIndex < 0 || _campDormant)
            return;
        if (NeedsFullPerformanceAi())
            tier = AgentLodTier.Near;
        if (_lodTier == tier)
            return;

        AgentLodTier previous = _lodTier;
        _lodTier = tier;
        if (tier == AgentLodTier.Far)
            EnterPerformanceSleep();
        else if (previous == AgentLodTier.Far)
            ExitPerformanceSleep();
    }

    // 被打或需要完整 AI 时强制唤醒所属营地。
    void ForceWakeFromPerformanceLod()
    {
        if (_perfCampIndex < 0)
            return;

        _perfSpawner?.WakeCamp(_perfCampIndex);
        _campDormant = false;
        _lodTier = AgentLodTier.Near;
        ExitPerformanceSleep();
    }

    // 战斗中、受击或现身时不能降 LOD。
    bool NeedsFullPerformanceAi()
    {
        return IsInCombat || _hitReacting || _waking || _attacking || _waitingToSpawn || _spawning;
    }

    // 普通营地怪才走性能 LOD。
    bool UsesPerformanceLod()
    {
        return _perfCampIndex >= 0 && !trainingDummy && !elite;
    }

    // 停 AI、暂停动作并隐藏血条。
    void EnterPerformanceSleep()
    {
        _chasing = false;
        _attacking = false;
        _patrolling = false;
        _idleVariationPlaying = false;
        ApplyMovement(Vector3.zero);
        if (_actions != null && !_actions.IsPaused)
            _actions.Pause();
        if (_healthBar != null)
            _healthBar.Hide();
    }

    // 从性能休眠恢复动作播放。
    void ExitPerformanceSleep()
    {
        if (_actions != null && _actions.IsPaused)
            _actions.Resume();
    }

    // 远档每帧只站住并保持暂停。
    void MaintainPerformanceSleep()
    {
        ApplyMovement(Vector3.zero);
        if (_actions != null && !_actions.IsPaused)
            _actions.Pause();
    }

    // 中档只处理击退/受击，其余走巡逻待机。
    void UpdateMidTierAi()
    {
        if (_knockback == null)
            _knockback = GetComponent<HitKnockback>();
        if (_knockback != null && _knockback.IsActive)
        {
            ApplyMovement(Vector3.zero);
            return;
        }

        if (_hitReacting)
        {
            ApplyMovement(Vector3.zero);
            if (!ActionFinished())
                return;
            _hitReacting = false;
        }

        UpdatePatrolOrIdle();
    }

    /// <summary>播放指定语义动作。</summary>
    public bool PlayAction(string actionId, bool restart = true)
    {
        return _actions.Play(actionId, restart);
    }

    // 锁定当前激活英雄作为目标。
    void AcquireTarget()
    {
        HeroController hero = HeroController.Active;
        if (hero == null || !hero.isActiveAndEnabled)
            hero = FindAnyObjectByType<HeroController>();
        _target = hero;
    }

    // 随机选近战招或投掷，并记下命中帧。
    void BeginAttack()
    {
        string actionId = HasRangedThrow
            ? throwAction
            : attackActions != null && attackActions.Length > 0
                ? attackActions[UnityEngine.Random.Range(0, attackActions.Length)]
                : "";
        if (string.IsNullOrEmpty(actionId) ||
            !_actions.TryGetAction(actionId, out CharacterActionDefinition attack))
            return;

        float duration = ActionPlayback(attack);
        _attacking = true;
        _attackApplied = false;
        _attackHitNormalized = HasRangedThrow
            ? 0.42f
            : AnimPlayback.ClipNormalized(attack.ImpactTime, attack.Clip);
        _nextAttackTime = Time.time + Mathf.Max(attackCooldown, duration * 0.55f);
        _idleVariationPlaying = false;
        _patrolling = false;
        _actions.Play(actionId, true);
    }

    // 近战命中玩家；已交战且还在视野里时不因打中次数回家睡觉。
    void ApplyMeleeHit(float distance)
    {
        if (_target == null || distance > attackRange * 1.35f)
            return;

        _target.TakeDamage(attackDamage, transform.position);
        _playerHitsLanded++;
        if (retreatAfterPlayerHits > 0 &&
            _playerHitsLanded >= retreatAfterPlayerHits &&
            ShouldReturnToSleep())
        {
            _attacking = false;
            BeginRetreat();
        }
    }

    // 从手上箭或预制体发射投射物。
    void LaunchProjectile()
    {
        if (_target == null || string.IsNullOrWhiteSpace(projectileResource))
            return;

        if (_handArrow == null)
            CacheHandArrow();

        Vector3 aim = _target.transform.position;
        if (_handArrow != null)
        {
            // 优先把手上那支箭直接射出去。
            Transform arrow = _handArrow;
            _handArrow = null;
            Vector3 handDirection = aim - arrow.position;
            handDirection.y = 0f;
            if (handDirection.sqrMagnitude < 0.01f)
                handDirection = transform.forward;
            SkeletonArrow.LaunchFromHand(
                arrow,
                handDirection.normalized,
                this,
                attackDamage,
                projectileRange,
                projectileSpeed,
                projectileStickDuration);
            _playerHitsLanded++;
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(projectileResource);
        if (prefab == null)
            return;

        // 手上没箭时从胸前生成一发。
        Vector3 origin = transform.position + Vector3.up * 1.25f + transform.forward * 0.7f;
        Vector3 launchDirection = aim - origin;
        launchDirection.y = 0f;
        if (launchDirection.sqrMagnitude < 0.01f)
            launchDirection = transform.forward;
        SkeletonArrow.Launch(
            prefab,
            origin,
            launchDirection.normalized,
            this,
            attackDamage,
            projectileRange,
            projectileSpeed,
            projectileStickDuration);
        _playerHitsLanded++;
    }

    // 隐藏模型，等玩家靠近再破土。
    void BeginWaitToSpawn()
    {
        _waitingToSpawn = true;
        _spawning = false;
        _chasing = false;
        SetVisualsVisible(false);
        if (_controller != null)
            _controller.enabled = false;
    }

    // 玩家进入触发范围则开始钻出。
    void UpdateWaitToSpawn()
    {
        if (_target == null || _target.IsDead)
            return;
        float distance = PlanarDistance(transform.position, _target.transform.position);
        if (distance <= spawnTriggerRange)
            BeginGroundSpawn();
    }

    // 显示模型并播放破土动画。
    void BeginGroundSpawn()
    {
        if (_spawning || _dead)
            return;

        _waitingToSpawn = false;
        _spawning = true;
        SetVisualsVisible(true);
        if (_controller != null)
            _controller.enabled = true;
        if (_actions != null && _actions.TryGetAction(spawnAction, out _))
        {
            _actions.Play(spawnAction, true);
        }
        else
        {
            FinishGroundSpawn();
        }
    }

    // 破土结束，进入追击。
    void FinishGroundSpawn()
    {
        _spawning = false;
        _chasing = true;
        if (_healthBar != null)
            _healthBar.Show();
        PlayIfAvailable(idleAction, true);
    }

    // 开关所有子网格渲染。
    void SetVisualsVisible(bool visible)
    {
        if (_visualRenderers == null || _visualRenderers.Length == 0)
            _visualRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in _visualRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    // 进入睡眠：站住、藏血条、清战斗状态。
    void EnterSleep()
    {
        _sleeping = true;
        _waking = false;
        _retreating = false;
        _fallingAsleep = false;
        _chasing = false;
        _attacking = false;
        _patrolling = false;
        _playerHitsLanded = 0;
        _combatEngaged = false;
        _hiddenFromViewSince = -1f;
        PlayIfAvailable(sleepAction, true);
        if (_healthBar != null)
            _healthBar.Hide();
        ApplyMovement(Vector3.zero);
    }

    // 被打醒：播醒来动画后开始追击。
    void BeginWake()
    {
        _sleeping = false;
        _fallingAsleep = false;
        _retreating = false;
        _waking = true;
        _combatEngaged = true;
        _hiddenFromViewSince = -1f;
        _playerHitsLanded = 0;
        if (_actions.TryGetAction(wakeAction, out _))
        {
            _actions.Play(wakeAction, true);
        }
        else
        {
            _waking = false;
            _chasing = true;
        }

        if (_healthBar != null)
            _healthBar.Show();
    }

    // 停止战斗，准备走回家。
    void BeginRetreat()
    {
        _retreating = true;
        _chasing = false;
        _attacking = false;
        _patrolling = false;
        _idleVariationPlaying = false;
    }

    // 走回家园点，到达后入睡；玩家又看见则重新交战。
    void UpdateRetreat()
    {
        if (_combatEngaged &&
            IsVisibleInPlayerView() &&
            _target != null &&
            !_target.IsDead)
        {
            _retreating = false;
            _chasing = true;
            _hiddenFromViewSince = -1f;
            return;
        }

        Vector3 offset = _homePosition - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance <= Mathf.Max(0.7f, patrolArriveDistance))
        {
            _retreating = false;
            if (_actions.TryGetAction(fallAsleepAction, out _))
            {
                _fallingAsleep = true;
                _actions.Play(fallAsleepAction, true);
                ApplyMovement(Vector3.zero);
                return;
            }

            EnterSleep();
            return;
        }

        Vector3 direction = offset / distance;
        Face(direction);
        ApplyMovement(direction * moveSpeed);
        PlayIfAvailable(moveAction);
    }

    // 睡眠怪交战后，只有离开玩家视野才回家睡觉。
    bool ShouldReturnToSleep()
    {
        if (!sleepUntilHit)
            return false;
        if (!_combatEngaged)
            return true;
        return HasLeftPlayerView();
    }

    bool HasLeftPlayerView()
    {
        if (IsVisibleInPlayerView())
        {
            _hiddenFromViewSince = -1f;
            return false;
        }

        if (_hiddenFromViewSince < 0f)
            _hiddenFromViewSince = Time.time;
        return Time.time - _hiddenFromViewSince >= 0.8f;
    }

    bool IsVisibleInPlayerView()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return true;

        float height = CharacterBodyFit.WorldHeight(transform);
        Vector3 point = transform.position + Vector3.up * Mathf.Max(0.4f, height * 0.45f);
        Vector3 viewport = camera.WorldToViewportPoint(point);
        return viewport.z > 0.15f &&
            viewport.x > -0.04f &&
            viewport.x < 1.04f &&
            viewport.y > -0.04f &&
            viewport.y < 1.04f;
    }

    // 关闭巡逻则纯待机，否则在等待与走动间切换。
    void UpdatePatrolOrIdle()
    {
        if (!patrol)
        {
            UpdateIdle();
            return;
        }

        if (_idleVariationPlaying)
        {
            ApplyMovement(Vector3.zero);
            if (!ActionFinished())
                return;

            _idleVariationPlaying = false;
            ScheduleIdleVariation();
        }

        if (_patrolling)
            UpdatePatrolWalk();
        else
            UpdatePatrolWait();
    }

    // 巡逻等待：太远先回家，否则到点再选下一个点。
    void UpdatePatrolWait()
    {
        ApplyMovement(Vector3.zero);

        float homeDistance = PlanarDistance(transform.position, _homePosition);
        if (homeDistance > EffectivePatrolRadius() * 1.2f)
        {
            BeginPatrolTo(_homePosition);
            return;
        }

        if (Time.time < _waitUntil)
        {
            TryPlayIdleVariation();
            if (!_idleVariationPlaying)
                PlayIfAvailable(idleAction);
            return;
        }

        if (!TryPickPatrolPoint(out Vector3 destination))
        {
            _waitUntil = Time.time + 1.2f;
            PlayIfAvailable(idleAction);
            return;
        }

        BeginPatrolTo(destination);
    }

    // 走向巡逻点，卡住或超时则结束这一段。
    void UpdatePatrolWalk()
    {
        Vector3 offset = _patrolPoint - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance <= patrolArriveDistance || Time.time >= _patrolGiveUpTime)
        {
            FinishPatrolLeg();
            return;
        }

        if (distance >= _lastProgressDistance - 0.04f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= 1.25f)
            {
                FinishPatrolLeg();
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
            _lastProgressDistance = distance;
        }

        Vector3 direction = offset / distance;
        Face(direction);
        ApplyMovement(direction * moveSpeed * Mathf.Max(0.2f, patrolSpeedMultiplier));
        PlayIfAvailable(moveAction);
    }

    // 开始走向一个巡逻点并设超时。
    void BeginPatrolTo(Vector3 destination)
    {
        _patrolPoint = destination;
        _patrolling = true;
        _stuckTimer = 0f;
        _lastProgressDistance = float.MaxValue;
        float distance = Mathf.Max(0.5f, PlanarDistance(transform.position, destination));
        float speed = Mathf.Max(0.4f, moveSpeed * patrolSpeedMultiplier);
        _patrolGiveUpTime = Time.time + distance / speed + 2.5f;
    }

    // 到达巡逻点后站住等待下一轮。
    void FinishPatrolLeg()
    {
        _patrolling = false;
        _waitUntil = Time.time + UnityEngine.Random.Range(patrolWait.x, Mathf.Max(patrolWait.x, patrolWait.y));
        PlayIfAvailable(idleAction);
        ApplyMovement(Vector3.zero);
    }

    // 在家园附近随机采样一块不太陡、不在水里的地面。
    bool TryPickPatrolPoint(out Vector3 destination)
    {
        float radius = EffectivePatrolRadius();
        for (int i = 0; i < 12; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * radius;
            if (circle.magnitude < Mathf.Min(2.5f, radius * 0.35f))
                continue;

            Vector3 candidate = _homePosition + new Vector3(circle.x, 0f, circle.y);
            if (!TrySampleGround(candidate, out destination, out Vector3 normal))
                continue;
            if (WaterProbe.IsInWater(destination))
                continue;
            if (Vector3.Angle(normal, Vector3.up) > 34f)
                continue;
            if (PlanarDistance(transform.position, destination) < patrolArriveDistance + 0.6f)
                continue;
            return true;
        }

        destination = _homePosition;
        return PlanarDistance(transform.position, _homePosition) > patrolArriveDistance;
    }

    // 到点则随机播一个待机变体，跳类动作给一点竖直速度。
    void TryPlayIdleVariation()
    {
        if (idleVariations == null ||
            idleVariations.Length == 0 ||
            Time.time < _nextIdleVariationTime)
            return;

        string variation = idleVariations[UnityEngine.Random.Range(0, idleVariations.Length)];
        if (_actions.TryGetAction(variation, out _))
        {
            _idleVariationPlaying = true;
            _actions.Play(variation, true);
            if (variation.IndexOf("jump", System.StringComparison.OrdinalIgnoreCase) >= 0)
                _verticalSpeed = 7.2f;
            return;
        }

        ScheduleIdleVariation();
    }

    // 巡逻半径至少覆盖体型。
    float EffectivePatrolRadius()
    {
        float height = 1.8f;
        if (_controller != null)
            height = Mathf.Max(1.2f, _controller.height * Mathf.Abs(transform.lossyScale.y));
        return Mathf.Max(patrolRadius, height * 1.6f);
    }

    // 忽略高度的平面距离。
    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 先采样地形高度，失败再向下射线。
    bool TrySampleGround(Vector3 xz, out Vector3 point, out Vector3 normal)
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
            point = new Vector3(xz.x, y, xz.z);
            normal = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
            return true;
        }

        if (Physics.Raycast(xz + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 160f))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = xz;
        normal = Vector3.up;
        return false;
    }

    // 原地待机并按间隔播变体动作。
    void UpdateIdle()
    {
        ApplyMovement(Vector3.zero);
        if (_idleVariationPlaying)
        {
            if (!ActionFinished())
                return;

            _idleVariationPlaying = false;
            ScheduleIdleVariation();
        }

        if (idleVariations != null &&
            idleVariations.Length > 0 &&
            Time.time >= _nextIdleVariationTime)
        {
            string variation = idleVariations[UnityEngine.Random.Range(0, idleVariations.Length)];
            if (_actions.TryGetAction(variation, out _))
            {
                _idleVariationPlaying = true;
                _actions.Play(variation, true);
                return;
            }
            ScheduleIdleVariation();
        }

        PlayIfAvailable(idleAction);
    }

    // 安排下一次待机变体时间。
    void ScheduleIdleVariation()
    {
        float maximum = Mathf.Max(idleVariationInterval.x, idleVariationInterval.y);
        _nextIdleVariationTime = Time.time +
            UnityEngine.Random.Range(idleVariationInterval.x, maximum);
    }

    // 转向给定平面方向。
    void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    // 施加平面速度、重力，并挡住走进水里。
    void ApplyMovement(Vector3 planarVelocity)
    {
        if (!_controller.enabled || !gameObject.activeInHierarchy)
            return;

        // 下一步会进水则停步并结束当前巡逻段。
        if (!IgnoresWater() && (planarVelocity.x != 0f || planarVelocity.z != 0f))
        {
            Vector3 next = transform.position +
                new Vector3(planarVelocity.x, 0f, planarVelocity.z) * Time.deltaTime * 3f;
            if (WaterProbe.IsInWater(next) || WaterProbe.CrossesWater(transform.position, next))
            {
                planarVelocity.x = 0f;
                planarVelocity.z = 0f;
                if (_patrolling)
                    FinishPatrolLeg();
            }
        }

        if (_controller.isGrounded && _verticalSpeed < 0f)
            _verticalSpeed = -2f;
        else
            _verticalSpeed += gravity * Time.deltaTime;

        planarVelocity.y = _verticalSpeed;
        _controller.Move(planarVelocity * Time.deltaTime);
    }

    // 睡眠怪与 Triton 可以进水。
    bool IgnoresWater()
    {
        return sleepUntilHit ||
            gameObject.name.IndexOf("triton", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // 配置里有该动作才播放。
    bool PlayIfAvailable(string actionId, bool restart = false)
    {
        if (!_actions.TryGetAction(actionId, out _))
            return false;
        return _actions.Play(actionId, restart);
    }

    // 按当前动画倍速算出动作时长。
    float ActionPlayback(CharacterActionDefinition action)
    {
        return AnimPlayback.Length(action, _animSpeed);
    }

    // 把木桩脚贴地并记下种植高度。
    void PlantDummy()
    {
        CharacterBodyFit.SnapFeetToGround(transform);
        _dummyPlantedY = transform.position.y;
        _dummyPlanted = true;
        _verticalSpeed = 0f;
    }

    // 把木桩拉回种植高度，避免被击退抬起。
    void KeepDummyPlanted()
    {
        if (!_dummyPlanted)
            PlantDummy();

        Vector3 position = transform.position;
        if (Mathf.Abs(position.y - _dummyPlantedY) <= 0.02f)
            return;

        bool wasEnabled = _controller != null && _controller.enabled;
        if (_controller != null)
            _controller.enabled = false;
        position.y = _dummyPlantedY;
        transform.position = position;
        if (_controller != null)
            _controller.enabled = wasEnabled;
    }

    // 头顶世界坐标，用于伤害飘字。
    Vector3 HeadPoint()
    {
        if (_controller != null && _controller.enabled)
            return new Vector3(transform.position.x, _controller.bounds.max.y, transform.position.z);
        if (CharacterBodyFit.TryMeasureWorldBounds(transform, out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        return transform.position + Vector3.up * 1.8f;
    }

    // 木桩血空后短暂等待再回满。
    IEnumerator RefillDummyHealth()
    {
        yield return new WaitForSeconds(0.45f);
        if (!trainingDummy || _dead)
            yield break;
        _health = maxHealth;
        NotifyHealth();
    }

    // 当前动作是否已播完。
    bool ActionFinished()
    {
        return _actions == null || _actions.PlaybackFinished;
    }

    // 小怪：播死亡、尸体飞出、特效块留下。精英：瞬间爆碎后销毁。
    IEnumerator DieAndBurst()
    {
        _dead = true;
        _attacking = false;
        _chasing = false;
        _idleVariationPlaying = false;
        _patrolling = false;
        _sleeping = false;
        _waking = false;
        _retreating = false;
        _fallingAsleep = false;
        NotifyHealth();
        PlayerProgress.NotifyKill(this);
        Died?.Invoke();

        var healthBar = GetComponentInChildren<WorldHealthBar>(true);
        if (healthBar != null)
            healthBar.Hide();

        if (!elite)
        {
            yield return DieWithFlyingCorpse();
            yield break;
        }

        if (_actions != null)
            _actions.Stop();

        SetVisualsVisible(false);
        SetCollidersEnabled(false);
        if (_controller != null && _controller.enabled)
            _controller.enabled = false;

        Vector3 burstCenter = BurstCenter();
        float burstScale = BurstScale();
        DeathBurst.Spawn(burstCenter, _lastHitOrigin, burstScale);

        yield return new WaitForSecondsRealtime(DeathBurst.CleanupDelay);
        if (this != null)
            Destroy(gameObject);
    }

    // 播死亡动作，沿最后一击把尸体打飞。
    IEnumerator DieWithFlyingCorpse()
    {
        if (_actions != null)
        {
            if (!_actions.Play(deathAction, true))
                _actions.Stop();
        }

        Vector3 fly = transform.position - _lastHitOrigin;
        fly.y = 0f;
        if (fly.sqrMagnitude < 0.0001f)
            fly = -transform.forward;
        fly.Normalize();
        float flyScale = Mathf.Max(0.05f, _deathFlyScale);
        Vector3 velocity = fly * (8.6f * flyScale) + Vector3.up * (6.4f * flyScale);
        float flyUntil = Time.time + Mathf.Lerp(0.38f, 0.9f, flyScale);
        bool leftGround = false;

        while (this != null && Time.time < flyUntil)
        {
            if (_controller == null || !_controller.enabled)
                break;

            if (!_controller.isGrounded)
                leftGround = true;
            velocity.y += gravity * Time.deltaTime;
            if (leftGround && _controller.isGrounded)
            {
                velocity.y = -2f;
                velocity.x *= 0.35f;
                velocity.z *= 0.35f;
            }

            _controller.Move(velocity * Time.deltaTime);
            yield return null;
        }

        float hold = Mathf.Max(corpseHoldDuration, 0.2f);
        if (_actions != null && !ActionFinished())
        {
            while (this != null && !ActionFinished())
                yield return null;
        }

        yield return new WaitForSeconds(hold);
        yield return FadeCorpseOut();
        if (this != null)
            Destroy(gameObject);
    }

    IEnumerator FadeCorpseOut()
    {
        float duration = Mathf.Max(0.2f, fadeDuration);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var blocks = new MaterialPropertyBlock[renderers.Length];
        var colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            blocks[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(blocks[i]);
            colors[i] = Color.white;
            Material material = renderers[i].sharedMaterial;
            if (material == null)
                continue;
            if (material.HasProperty("_BaseColor"))
                colors[i] = material.GetColor("_BaseColor");
            else if (material.HasProperty("_Color"))
                colors[i] = material.GetColor("_Color");
        }

        float elapsed = 0f;
        while (elapsed < duration && this != null)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                Color color = colors[i];
                color.a = alpha;
                blocks[i].SetColor("_BaseColor", color);
                blocks[i].SetColor("_Color", color);
                renderers[i].SetPropertyBlock(blocks[i]);
            }

            yield return null;
        }
    }

    Vector3 BurstCenter()
    {
        if (CharacterBodyFit.TryMeasureBodyBounds(transform, out Bounds bounds))
            return bounds.center;
        float height = CharacterBodyFit.WorldHeight(transform);
        return transform.position + Vector3.up * height * 0.45f;
    }

    float BurstScale()
    {
        if (CharacterBodyFit.TryMeasureBodyBounds(transform, out Bounds bounds))
            return Mathf.Clamp(bounds.extents.magnitude * 0.55f, 0.65f, 2.2f);
        return Mathf.Clamp(CharacterBodyFit.WorldHeight(transform) * 0.28f, 0.65f, 2.2f);
    }

    void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    // 在编辑器中画警戒、攻击与巡逻圈。
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0.35f, 0.85f, 0.4f, 0.9f);
        Vector3 home = Application.isPlaying ? _homePosition : transform.position;
        Gizmos.DrawWireSphere(home, EffectivePatrolRadius());
    }
}
