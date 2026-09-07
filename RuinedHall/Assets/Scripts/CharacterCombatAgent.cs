using System;
using System.Collections;
using UnityEngine;

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

    public bool IsDead => _dead;
    public bool IsPaused => _paused;
    public bool IsInCombat => _chasing || _attacking || _retreating;
    public bool IsElite => elite;
    public bool IsTrainingDummy => trainingDummy;
    public string EliteTitle => eliteTitle;
    public int GoldReward => goldReward;
    public int CurrentHealth => _health;
    public int MaxHealth => maxHealth;
    public CharacterActionPlayer Actions => _actions;
    public event Action<int, int> HealthChanged;
    public event Action Died;

    void Awake()
    {
        _actions = GetComponent<CharacterActionPlayer>();
        _controller = GetComponent<CharacterController>();
        _knockback = GetComponent<HitKnockback>();
        EnemyIdentity.Apply(this);
        _health = maxHealth;
        NotifyHealth();
    }

    public void ApplyRewards(bool isElite, string title, int gold)
    {
        elite = isElite;
        eliteTitle = title ?? "";
        goldReward = Mathf.Max(0, gold);
    }

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

    public void ApplyIdleHabits(string[] variations, Vector2 interval, float radius)
    {
        idleVariations = variations ?? System.Array.Empty<string>();
        idleVariationInterval = interval;
        patrol = true;
        patrolRadius = Mathf.Max(2f, radius);
    }

    public void ApplyPassiveUntilHit(bool value)
    {
        passiveUntilHit = value;
    }

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

    public void ApplyChargeOnAttack(bool value)
    {
        chargeOnAttack = value;
    }

    public void ForceChase()
    {
        _chasing = true;
        _patrolling = false;
        _idleVariationPlaying = false;
        AcquireTarget();
    }

    public void ApplyAttackActions(params string[] actions)
    {
        attackActions = actions ?? System.Array.Empty<string>();
    }

    public void ApplySpawnFromGround(float triggerRange)
    {
        spawnFromGround = true;
        spawnAction = "Spawn";
        spawnTriggerRange = Mathf.Max(4f, triggerRange);
    }

    public void ApplyRangedThrow(string resource, float flightRange, float speed)
    {
        throwAction = "Throw";
        projectileResource = resource ?? "";
        projectileRange = Mathf.Max(4f, flightRange);
        projectileSpeed = Mathf.Max(4f, speed);
        projectileStickDuration = 1f;
    }

    bool HasRangedThrow =>
        !string.IsNullOrWhiteSpace(projectileResource) &&
        (_actions == null || _actions.TryGetAction(throwAction, out _));

    void NotifyHealth()
    {
        HealthChanged?.Invoke(_health, maxHealth);
    }

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

        _healthBar = WorldHealthBar.Attach(this);
        if ((sleepUntilHit || spawnFromGround) && _healthBar != null)
            _healthBar.Hide();
        if (HasRangedThrow)
            CacheHandArrow();
        NotifyHealth();
    }

    void LateUpdate()
    {
        if (trainingDummy && _dummyPlantFrames < 24)
        {
            PlantDummy();
            _dummyPlantFrames++;
        }

        UpdateEliteRangeVisual();
    }

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

    void OnDestroy()
    {
        if (_attackRange != null)
            Destroy(_attackRange.gameObject);
    }

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

    void Update()
    {
        if (_dead || _paused)
            return;

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

        if (trainingDummy)
        {
            KeepDummyPlanted();
            if (!_hitReacting)
                PlayIfAvailable(idleAction);
            return;
        }

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

        if (passiveUntilHit && !_chasing)
        {
            UpdatePatrolOrIdle();
            return;
        }

        if (_target == null || _target.IsDead)
        {
            _chasing = false;
            _attacking = false;
            if (sleepUntilHit)
                BeginRetreat();
            else
                UpdatePatrolOrIdle();
            return;
        }

        Vector3 offset = _target.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

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

        _chasing = distance <= detectionRange || (_chasing && distance <= loseInterestRange);
        if (!_chasing || distance < 0.01f)
        {
            if (sleepUntilHit)
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

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position + transform.forward);
    }

    public void TakeDamage(int amount, Vector3 hitOrigin)
    {
        if (_dead || amount <= 0)
            return;

        if (_waitingToSpawn)
            BeginGroundSpawn();

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
        HitKnockback.ApplyTo(this, hitOrigin, _health == 0 ? 1.4f : 1f);
        _knockback = GetComponent<HitKnockback>();
        if (_health == 0)
        {
            StartCoroutine(DieAndFade());
            return;
        }

        if (sleepUntilHit && (_sleeping || _fallingAsleep))
        {
            BeginWake();
            return;
        }

        _chasing = true;

        _attacking = false;
        if (_actions.TryGetAction(hitAction, out _))
        {
            _actions.Play(hitAction, true);
            _hitReacting = true;
        }
    }

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

    public void PauseCharacter()
    {
        _paused = true;
        _actions.Pause();
    }

    public void ResumeCharacter()
    {
        _paused = false;
        _actions.Resume();
    }

    public bool PlayAction(string actionId, bool restart = true)
    {
        return _actions.Play(actionId, restart);
    }

    void AcquireTarget()
    {
        HeroController hero = HeroController.Active;
        if (hero == null || !hero.isActiveAndEnabled)
            hero = FindAnyObjectByType<HeroController>();
        _target = hero;
    }

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

    void ApplyMeleeHit(float distance)
    {
        if (_target == null || distance > attackRange * 1.35f)
            return;

        _target.TakeDamage(attackDamage, transform.position);
        _playerHitsLanded++;
        if (retreatAfterPlayerHits > 0 &&
            _playerHitsLanded >= retreatAfterPlayerHits)
        {
            _attacking = false;
            BeginRetreat();
        }
    }

    void LaunchProjectile()
    {
        if (_target == null || string.IsNullOrWhiteSpace(projectileResource))
            return;

        if (_handArrow == null)
            CacheHandArrow();

        Vector3 aim = _target.transform.position;
        if (_handArrow != null)
        {
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

    void BeginWaitToSpawn()
    {
        _waitingToSpawn = true;
        _spawning = false;
        _chasing = false;
        SetVisualsVisible(false);
        if (_controller != null)
            _controller.enabled = false;
    }

    void UpdateWaitToSpawn()
    {
        if (_target == null || _target.IsDead)
            return;
        float distance = PlanarDistance(transform.position, _target.transform.position);
        if (distance <= spawnTriggerRange)
            BeginGroundSpawn();
    }

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

    void FinishGroundSpawn()
    {
        _spawning = false;
        _chasing = true;
        if (_healthBar != null)
            _healthBar.Show();
        PlayIfAvailable(idleAction, true);
    }

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
        PlayIfAvailable(sleepAction, true);
        if (_healthBar != null)
            _healthBar.Hide();
        ApplyMovement(Vector3.zero);
    }

    void BeginWake()
    {
        _sleeping = false;
        _fallingAsleep = false;
        _retreating = false;
        _waking = true;
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

    void BeginRetreat()
    {
        _retreating = true;
        _chasing = false;
        _attacking = false;
        _patrolling = false;
        _idleVariationPlaying = false;
    }

    void UpdateRetreat()
    {
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

    void FinishPatrolLeg()
    {
        _patrolling = false;
        _waitUntil = Time.time + UnityEngine.Random.Range(patrolWait.x, Mathf.Max(patrolWait.x, patrolWait.y));
        PlayIfAvailable(idleAction);
        ApplyMovement(Vector3.zero);
    }

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

    float EffectivePatrolRadius()
    {
        float height = 1.8f;
        if (_controller != null)
            height = Mathf.Max(1.2f, _controller.height * Mathf.Abs(transform.lossyScale.y));
        return Mathf.Max(patrolRadius, height * 1.6f);
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

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

    void ScheduleIdleVariation()
    {
        float maximum = Mathf.Max(idleVariationInterval.x, idleVariationInterval.y);
        _nextIdleVariationTime = Time.time +
            UnityEngine.Random.Range(idleVariationInterval.x, maximum);
    }

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

    void ApplyMovement(Vector3 planarVelocity)
    {
        if (!_controller.enabled || !gameObject.activeInHierarchy)
            return;

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

    bool IgnoresWater()
    {
        return sleepUntilHit ||
            gameObject.name.IndexOf("triton", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool PlayIfAvailable(string actionId, bool restart = false)
    {
        if (!_actions.TryGetAction(actionId, out _))
            return false;
        return _actions.Play(actionId, restart);
    }

    float ActionPlayback(CharacterActionDefinition action)
    {
        return AnimPlayback.Length(action, _animSpeed);
    }

    void PlantDummy()
    {
        CharacterBodyFit.SnapFeetToGround(transform);
        _dummyPlantedY = transform.position.y;
        _dummyPlanted = true;
        _verticalSpeed = 0f;
    }

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

    Vector3 HeadPoint()
    {
        if (_controller != null && _controller.enabled)
            return new Vector3(transform.position.x, _controller.bounds.max.y, transform.position.z);
        if (CharacterBodyFit.TryMeasureWorldBounds(transform, out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        return transform.position + Vector3.up * 1.8f;
    }

    IEnumerator RefillDummyHealth()
    {
        yield return new WaitForSeconds(0.45f);
        if (!trainingDummy || _dead)
            yield break;
        _health = maxHealth;
        NotifyHealth();
    }

    bool ActionFinished()
    {
        return _actions == null || _actions.PlaybackFinished;
    }

    IEnumerator DieAndFade()
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

        float deathDuration = 0f;
        if (_actions.TryGetAction(deathAction, out CharacterActionDefinition death))
        {
            deathDuration = ActionPlayback(death);
            _actions.Play(deathAction, true);
        }

        yield return new WaitForSeconds(0.22f);
        if (_controller.enabled)
            _controller.enabled = false;

        yield return new WaitForSeconds(Mathf.Max(0f, deathDuration + corpseHoldDuration - 0.22f));
        yield return CharacterFadeUtility.FadeAndDestroy(transform, fadeDuration);
    }

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
