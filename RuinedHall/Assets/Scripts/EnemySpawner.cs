using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    sealed class Camp
    {
        public Vector3 Center;
        public int Capacity;
        public readonly List<CharacterCombatAgent> Spawned = new();
        public int Pending;
    }

    readonly List<Camp> _camps = new();

    public void Initialize(GameObject roosterTemplate, Transform heroTransform)
    {
        template = roosterTemplate;
        hero = heroTransform;
    }

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

        foreach (GameObject marker in markers)
        {
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

        foreach (Camp camp in _camps)
        {
            for (int i = 0; i < camp.Capacity; i++)
                SpawnAtCamp(camp);
        }
    }

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

    void SpawnAtCamp(Camp camp)
    {
        if (template == null || AliveIn(camp) + camp.Pending >= camp.Capacity)
            return;

        if (!TryFindPoint(camp, out Vector3 point))
        {
            camp.Pending++;
            StartCoroutine(RetrySpawn(camp));
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
    }

    IEnumerator RetrySpawn(Camp camp)
    {
        yield return new WaitForSeconds(1.2f);
        camp.Pending = Mathf.Max(0, camp.Pending - 1);
        SpawnAtCamp(camp);
    }

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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.35f);
        foreach (Camp camp in _camps)
            Gizmos.DrawWireSphere(camp.Center, campRadius);
    }
}
