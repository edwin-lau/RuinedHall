using System;
using UnityEngine;

/// <summary>
/// 敌人死亡爆碎：纯粒子喷发，按来向定向，同屏多杀时自动降粒子预算。
/// </summary>
public static class DeathBurst
{
    const float CleanupSeconds = 1.05f;
    const int MaxFullBursts = 5;

    static int _activeBursts;
    static Material _sharedMaterial;

    /// <summary>粒子播完后销毁敌人根物体的等待时间。</summary>
    public static float CleanupDelay => CleanupSeconds;

    /// <summary>在中心点沿最后一击来向喷发尘土/碎屑/血雾。</summary>
    public static void Spawn(Vector3 center, Vector3 hitOrigin, float scale)
    {
        Vector3 direction = center - hitOrigin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;
        else
            direction.Normalize();

        _activeBursts++;
        float budget = _activeBursts <= MaxFullBursts
            ? 1f
            : Mathf.Clamp(MaxFullBursts / (float)_activeBursts, 0.35f, 1f);
        scale = Mathf.Clamp(scale, 0.55f, 2.4f);

        var root = new GameObject("DeathBurst");
        root.transform.position = center;
        root.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        int dustCount = Mathf.Max(4, Mathf.RoundToInt(14 * budget));
        int debrisCount = Mathf.Max(3, Mathf.RoundToInt(10 * budget));
        int mistCount = Mathf.Max(2, Mathf.RoundToInt(7 * budget));

        AddConeBurst(
            root.transform,
            "Dust",
            new Color(0.62f, 0.5f, 0.36f, 0.82f),
            scale * 0.42f,
            scale * 1.35f,
            0.72f,
            34f,
            dustCount,
            0.08f);
        AddConeBurst(
            root.transform,
            "Debris",
            new Color(0.34f, 0.28f, 0.22f, 0.9f),
            scale * 0.18f,
            scale * 2.8f,
            0.42f,
            22f,
            debrisCount,
            0.04f);
        AddConeBurst(
            root.transform,
            "Mist",
            new Color(0.72f, 0.14f, 0.1f, 0.45f),
            scale * 0.55f,
            scale * 1.9f,
            0.58f,
            42f,
            mistCount,
            0.12f);

        var tracker = root.AddComponent<DeathBurstTracker>();
        tracker.Begin(CleanupSeconds + 0.15f, ReleaseBurst);
        UnityEngine.Object.Destroy(root, CleanupSeconds + 0.2f);
    }

    static void ReleaseBurst()
    {
        _activeBursts = Mathf.Max(0, _activeBursts - 1);
    }

    static void AddConeBurst(
        Transform parent,
        string label,
        Color color,
        float size,
        float speed,
        float lifetime,
        float coneAngle,
        int count,
        float radius)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);

        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count + 2, 24);
        main.gravityModifier = label == "Debris" ? 0.55f : 0.15f;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = radius * parent.lossyScale.x;
        shape.rotation = Vector3.zero;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        Material material = GetSharedMaterial();
        if (material != null)
            renderer.material = material;

        particles.Play(true);
    }

    static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        _sharedMaterial = new Material(shader);
        return _sharedMaterial;
    }

    sealed class DeathBurstTracker : MonoBehaviour
    {
        float _until;
        Action _onDone;

        public void Begin(float seconds, Action onDone)
        {
            _until = Time.unscaledTime + seconds;
            _onDone = onDone;
        }

        void Update()
        {
            if (_onDone == null || Time.unscaledTime < _until)
                return;
            _onDone.Invoke();
            _onDone = null;
        }

        void OnDestroy()
        {
            if (_onDone == null)
                return;
            _onDone.Invoke();
            _onDone = null;
        }
    }
}
