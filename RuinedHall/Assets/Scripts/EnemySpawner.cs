using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按场景标记点生成公鸡营地，并按与英雄的距离做性能 LOD。
/// </summary>
public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject template;
    [SerializeField] Transform hero;
    [SerializeField] int minPerCamp = 7;
    [SerializeField] int maxPerCamp = 10;
    [SerializeField] float campRadius = 9.5f;
    [SerializeField] float minSeparation = 2.2f;
    [SerializeField] float maxSlope = 32f;
    [SerializeField] int sampleAttempts = 28;
    [Header("Performance LOD")]
    [SerializeField] float campWakeRadius = 42f;
    [SerializeField] float campSleepRadius = 52f;
    [SerializeField] float nearTierRadius = 25f;
    [SerializeField] float midTierRadius = 50f;

    /// <summary>单个营地：中心、容量、唤醒状态与已生成敌人。</summary>
    sealed class Camp
    {
        public Vector3 Center;
        public int Capacity;
        public bool Active;
        public readonly List<CharacterCombatAgent> Spawned = new();
        public int Pending;
    }

    readonly List<Camp> _camps = new();

    /// <summary>绑定公鸡模板与英雄 Transform。</summary>
    public void Initialize(GameObject roosterTemplate, Transform heroTransform)
    {
        template = roosterTemplate;
        hero = heroTransform;
    }

    /// <summary>立即唤醒指定营地，使其进入近距离 AI。</summary>
    public void WakeCamp(int campIndex)
    {
        if (campIndex < 0 || campIndex >= _camps.Count)
            return;

        _camps[campIndex].Active = true;
    }

    // 收集刷新点、建营地并一次生成全部公鸡。
    void Start()
    {
        var markers = CollectRoosterMarkers();
        if (markers.Count == 0)
        {
            Debug.LogError("EnemySpawner: 找不到公鸡刷新点。");
            enabled = false;
            return;
        }

        template = markers[0];
        if (hero == null)
        {
            var heroController = FindAnyObjectByType<HeroController>();
            if (heroController != null)
                hero = heroController.transform;
        }

        // 每个标记点落成一个营地，标记物本身关掉以免重复当敌人。
        for (int i = 0; i < markers.Count; i++)
        {
            GameObject marker = markers[i];
            Vector3 center = marker.transform.position;
            if (TrySampleGround(center, out Vector3 grounded, out _) && !WaterProbe.IsInWater(grounded))
                center = grounded;
            _camps.Add(new Camp
            {
                Center = center,
                Capacity = UnityEngine.Random.Range(minPerCamp, maxPerCamp + 1)
            });
            marker.SetActive(false);
        }

        // 按容量在每个营地生成公鸡。
        for (int campIndex = 0; campIndex < _camps.Count; campIndex++)
        {
            Camp camp = _camps[campIndex];
            for (int i = 0; i < camp.Capacity; i++)
                SpawnAtCamp(camp, campIndex);
        }

        UpdatePerformanceLod();
    }

    // 每帧按英雄距离刷新营地 LOD。
    void Update()
    {
        UpdatePerformanceLod();
    }

    // 按唤醒/休眠半径切换营地，再给每个敌人设 LOD 档。
    void UpdatePerformanceLod()
    {
        if (hero == null)
            return;

        Vector3 heroPos = hero.position;
        for (int campIndex = 0; campIndex < _camps.Count; campIndex++)
        {
            Camp camp = _camps[campIndex];
            float campDistance = PlanarDistance(camp.Center, heroPos);
            if (!camp.Active && campDistance <= campWakeRadius)
                camp.Active = true;
            else if (camp.Active && campDistance >= campSleepRadius)
                camp.Active = false;

            ApplyCampPerformance(camp, heroPos);
        }
    }

    // 休眠营地点名休眠；唤醒营地再按距离分近/中/远档。
    void ApplyCampPerformance(Camp camp, Vector3 heroPos)
    {
        for (int i = 0; i < camp.Spawned.Count; i++)
        {
            CharacterCombatAgent agent = camp.Spawned[i];
            if (agent == null || agent.IsDead)
                continue;

            if (!camp.Active)
            {
                agent.SetCampDormant(true);
                continue;
            }

            agent.SetCampDormant(false);
            float distance = PlanarDistance(agent.transform.position, heroPos);
            if (distance <= nearTierRadius)
                agent.SetLodTier(CharacterCombatAgent.AgentLodTier.Near);
            else if (distance <= midTierRadius)
                agent.SetLodTier(CharacterCombatAgent.AgentLodTier.Mid);
            else
                agent.SetLodTier(CharacterCombatAgent.AgentLodTier.Far);
        }
    }

    // 在场景里找名字含 rooster 的标记点作为营地中心。
    static List<GameObject> CollectRoosterMarkers()
    {
        var markers = new List<GameObject>();
        foreach (var agent in FindObjectsByType<CharacterCombatAgent>(FindObjectsInactive.Exclude))
        {
            if (agent == null)
                continue;
            if (agent.gameObject.name.IndexOf("rooster", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            markers.Add(agent.gameObject);
        }

        if (markers.Count == 0)
        {
            GameObject named = GameObject.Find("rooster");
            if (named != null)
                markers.Add(named);
        }

        return markers;
    }

    // 在营地内采样落点并实例化一只公鸡；失败则稍后重试。
    void SpawnAtCamp(Camp camp, int campIndex)
    {
        if (template == null || AliveIn(camp) + camp.Pending >= camp.Capacity)
            return;

        if (!TryFindPoint(camp, out Vector3 point))
        {
            camp.Pending++;
            StartCoroutine(RetrySpawn(camp, campIndex));
            return;
        }

        GameObject instance = Instantiate(
            template,
            point,
            Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        instance.name = "rooster";
        instance.SetActive(true);

        var agent = instance.GetComponent<CharacterCombatAgent>();
        if (agent == null)
            return;

        camp.Spawned.Add(agent);
        agent.BindPerformanceCamp(this, campIndex, camp.Spawned.Count - 1);
    }

    // 短暂等待后再次尝试在该营地生成。
    IEnumerator RetrySpawn(Camp camp, int campIndex)
    {
        yield return new WaitForSeconds(1.2f);
        camp.Pending = Mathf.Max(0, camp.Pending - 1);
        SpawnAtCamp(camp, campIndex);
    }

    // 统计营地中仍存活的敌人数量。
    static int AliveIn(Camp camp)
    {
        int count = 0;
        for (int i = 0; i < camp.Spawned.Count; i++)
        {
            if (camp.Spawned[i] != null && !camp.Spawned[i].IsDead)
                count++;
        }

        return count;
    }

    // 在营地半径内采样合法地面点，避开水和过近邻居。
    bool TryFindPoint(Camp camp, out Vector3 point)
    {
        for (int i = 0; i < sampleAttempts; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * campRadius;
            Vector3 candidate = camp.Center + new Vector3(circle.x, 0f, circle.y);
            if (!TrySampleGround(candidate, out point, out Vector3 normal))
                continue;
            if (WaterProbe.IsInWater(point))
                continue;
            if (Vector3.Angle(normal, Vector3.up) > maxSlope)
                continue;
            if (IsOccupied(point))
                continue;
            return true;
        }

        point = camp.Center;
        return !WaterProbe.IsInWater(point) && !IsOccupied(point);
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
            normal = terrain.terrainData.GetInterpolatedNormal(
                local.x / size.x,
                local.z / size.z);
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

    // 该点是否已有活着的敌人挤在最小间距内。
    bool IsOccupied(Vector3 point)
    {
        float minSqr = minSeparation * minSeparation;
        for (int c = 0; c < _camps.Count; c++)
        {
            List<CharacterCombatAgent> spawned = _camps[c].Spawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                CharacterCombatAgent agent = spawned[i];
                if (agent == null || agent.IsDead)
                    continue;
                if ((agent.transform.position - point).sqrMagnitude < minSqr)
                    return true;
            }
        }

        return false;
    }

    // 忽略高度的平面距离。
    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 在编辑器中画出营地半径与唤醒/休眠圈。
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.35f);
        foreach (Camp camp in _camps)
            Gizmos.DrawWireSphere(camp.Center, campRadius);

        Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.25f);
        foreach (Camp camp in _camps)
            Gizmos.DrawWireSphere(camp.Center, campWakeRadius);

        Gizmos.color = new Color(0.85f, 0.2f, 0.2f, 0.2f);
        foreach (Camp camp in _camps)
            Gizmos.DrawWireSphere(camp.Center, campSleepRadius);
    }
}
