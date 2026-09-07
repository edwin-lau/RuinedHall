using System.Collections.Generic;
using UnityEngine;

public static class WaterProbe
{
    static readonly List<Transform> Bodies = new();
    static float _seaLevel;
    static bool _hasSeaLevel;
    static bool _initialized;

    public static float SeaLevel => _hasSeaLevel ? _seaLevel : 0f;

    public static bool IsInWater(Vector3 point)
    {
        RefreshIfNeeded();
        if (!_hasSeaLevel && Bodies.Count == 0)
            return false;

        for (int i = 0; i < Bodies.Count; i++)
        {
            Transform body = Bodies[i];
            if (body == null || !ContainsXz(body, point, 1.2f))
                continue;
            if (point.y <= SurfaceY() + 0.7f)
                return true;
        }

        return false;
    }

    public static bool CrossesWater(Vector3 from, Vector3 to)
    {
        for (int i = 1; i <= 6; i++)
        {
            if (IsInWater(Vector3.Lerp(from, to, i / 6f)))
                return true;
        }

        return false;
    }

    public static bool TryFindLand(Vector3 from, float radius, out Vector3 land)
    {
        RefreshIfNeeded();
        for (int ring = 0; ring < 3; ring++)
        {
            float distance = radius * (0.55f + ring * 0.45f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * (360f / 16f);
                Vector3 candidate = from + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                if (IsInWater(candidate))
                    continue;
                land = candidate;
                return true;
            }
        }

        land = from;
        return !IsInWater(from);
    }

    public static Vector3 EscapeDirection(Vector3 point, Vector3 incoming)
    {
        RefreshIfNeeded();
        incoming.y = 0f;
        Vector3 away = Vector3.zero;
        for (int i = 0; i < Bodies.Count; i++)
        {
            Transform body = Bodies[i];
            if (body == null || !ContainsXz(body, point, 4f))
                continue;
            Vector3 delta = point - body.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f)
                delta = incoming.sqrMagnitude > 0.01f ? -incoming : Vector3.forward;
            away += delta.normalized;
        }

        if (away.sqrMagnitude > 0.01f)
            return away.normalized;
        if (incoming.sqrMagnitude > 0.01f)
            return -incoming.normalized;
        return Vector3.forward;
    }

    static float SurfaceY()
    {
        return _hasSeaLevel ? _seaLevel : 7.3f;
    }

    static bool ContainsXz(Transform body, Vector3 point, float padding)
    {
        Vector3 center = body.position;
        Vector3 half = body.lossyScale * 0.5f;
        return Mathf.Abs(point.x - center.x) <= Mathf.Abs(half.x) + padding &&
               Mathf.Abs(point.z - center.z) <= Mathf.Abs(half.z) + padding;
    }

    static void RefreshIfNeeded()
    {
        if (_initialized)
            return;

        _initialized = true;
        Bodies.Clear();
        _hasSeaLevel = false;
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
        {
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "OceanRenderer")
            {
                _seaLevel = behaviour.transform.position.y;
                _hasSeaLevel = true;
                continue;
            }

            if (typeName != "WaterBody")
                continue;
            if (behaviour.gameObject.name.IndexOf("Ocean", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            Bodies.Add(behaviour.transform);
        }
    }
}
