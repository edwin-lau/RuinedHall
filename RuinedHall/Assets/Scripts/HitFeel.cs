using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 命中手感：出拳音效、顿帧、镜头踢、受击闪白与打击特效。
/// </summary>
public sealed class HitFeel : MonoBehaviour
{
    static readonly float[] StopSeconds = { 0.05f, 0.065f, 0.08f, 0.13f };
    static readonly float[] KickMeters = { 0.035f, 0.048f, 0.062f, 0.09f };
    static readonly float[] KnockbackScale = { 0.38f, 0.52f, 0.68f, 0.9f };
    // Punch4 为当前死亡击飞；Punch3 为其一半；Punch1/2 为 Punch3 的 1/3 与 2/3。
    static readonly float[] DeathFlyScale = { 1f / 6f, 1f / 3f, 0.5f, 1f };
    static readonly string[] CasualPrefabs =
    {
        "Assets/Casual_Hit/Prefabs/Hit_1/Hit_1_Normal.prefab",
        "Assets/Casual_Hit/Prefabs/Hit_1/Hit_1_Normal.prefab",
        "Assets/Casual_Hit/Prefabs/Hit_3/Hit_3_Yellow.prefab",
        "Assets/Casual_Hit/Prefabs/Hit_4/Hit_4_Yellow.prefab"
    };
    const string PunchEmptyPath = "characters/hero2/audio/O_punchEmpty";
    static readonly string[] PunchHitPaths =
    {
        "characters/hero2/audio/O_punch1",
        "characters/hero2/audio/O_punch2",
        "characters/hero2/audio/O_punch3",
        "characters/hero2/audio/O_punch4"
    };
    const string DefaultHitClipPath = "characters/hero2/audio/hitOk";
    // 在代码里裁剪：起点秒数；时长 0 表示播到结尾。Unity Inspector 不能剪 wav。
    const float DefaultHitClipStart = 0f;
    const float DefaultHitClipDuration = 0f;

    static HitFeel _instance;
    AudioSource _audio;
    AudioClip _whoosh;
    AudioClip _impact;
    AudioClip[] _punchHits;
    Coroutine _stopRoutine;
    float _savedTimeScale = 1f;
    readonly List<FlashState> _flashes = new();

    /// <summary>一次闪白：哪个网格、何时还原。</summary>
    struct FlashState
    {
        public Renderer Renderer;
        public float Until;
    }

    /// <summary>确保场景中存在全局 HitFeel 单例。</summary>
    public static HitFeel Ensure()
    {
        if (_instance != null)
            return _instance;

        var go = new GameObject("HitFeel");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HitFeel>();
        return _instance;
    }

    // 创建 2D 音源并生成挥拳/命中噪声片段。
    void Awake()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.ignoreListenerPause = true;
        _whoosh = LoadClip(PunchEmptyPath)
            ?? MakeNoiseClip("Whoosh", 0.11f, 420f, 0.22f, true);
        _punchHits = new AudioClip[PunchHitPaths.Length];
        for (int i = 0; i < PunchHitPaths.Length; i++)
            _punchHits[i] = LoadClip(PunchHitPaths[i]);
        _impact = _punchHits[0]
            ?? LoadSlicedClip(DefaultHitClipPath, DefaultHitClipStart, DefaultHitClipDuration)
            ?? MakeNoiseClip("Impact", 0.09f, 90f, 0.55f, false);
    }

    /// <summary>播放出拳挥空音效。</summary>
    public static void PunchSwing()
    {
        Ensure().PlayClip(Ensure()._whoosh, 0.55f);
    }

    /// <summary>按连招段返回击退缩放。</summary>
    public static float PunchKnockback(int comboIndex)
    {
        return KnockbackScale[Mathf.Clamp(comboIndex, 0, KnockbackScale.Length - 1)];
    }

    /// <summary>按连招段返回普通怪死亡击飞缩放；1 为当前 Punch4 距离。</summary>
    public static float PunchDeathFly(int comboIndex)
    {
        return DeathFlyScale[Mathf.Clamp(comboIndex, 0, DeathFlyScale.Length - 1)];
    }

    /// <summary>拳头命中时触发顿帧、镜头踢、闪白与特效。</summary>
    public static void PunchConnected(
        int comboIndex,
        Vector3 origin,
        Vector3 forward,
        ICollection<CharacterCombatAgent> victims)
    {
        Ensure().PlayConnected(comboIndex, origin, forward, victims);
    }

    // 按连招段播放命中音效、顿帧、镜头踢、受击闪白和打击特效。
    void PlayConnected(
        int comboIndex,
        Vector3 origin,
        Vector3 forward,
        ICollection<CharacterCombatAgent> victims)
    {
        int stage = Mathf.Clamp(comboIndex, 0, StopSeconds.Length - 1);
        AudioClip hitClip = _punchHits != null &&
            stage < _punchHits.Length &&
            _punchHits[stage] != null
            ? _punchHits[stage]
            : _impact;
        PlayClip(hitClip, 0.7f + stage * 0.12f);
        BeginStop(StopSeconds[stage]);
        KickCamera(forward, KickMeters[stage]);

        // 命中点取在拳到胸口之间，并对每个受害者闪白。
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 contact = origin + forward * 0.7f + Vector3.up * 1.05f;
        if (victims != null)
        {
            foreach (CharacterCombatAgent victim in victims)
            {
                if (victim == null)
                    continue;
                Vector3 chest = victim.transform.position + Vector3.up * 1.05f;
                contact = Vector3.Lerp(origin + Vector3.up * 1.0f, chest, 0.55f);
                Flash(victim.gameObject, 0.055f + stage * 0.008f);
            }
        }

        SpawnImpact(contact, 0.85f + stage * 0.18f, stage);
    }

    /// <summary>从 Resources 加载整段音频。</summary>
    static AudioClip LoadClip(string resourcePath)
    {
        return Resources.Load<AudioClip>(resourcePath);
    }

    /// <summary>从 Resources 加载音频，并按起点/时长裁出一段新 clip。</summary>
    static AudioClip LoadSlicedClip(string resourcePath, float startSeconds, float durationSeconds)
    {
        AudioClip source = Resources.Load<AudioClip>(resourcePath);
        if (source == null)
            return null;
        if (!source.LoadAudioData())
            return source;
        return SliceClip(source, startSeconds, durationSeconds) ?? source;
    }

    /// <summary>复制指定时间范围；duration≤0 表示从 start 播到结尾。</summary>
    static AudioClip SliceClip(AudioClip source, float startSeconds, float durationSeconds)
    {
        if (source == null || source.samples <= 0)
            return source;

        int channels = Mathf.Max(1, source.channels);
        int frequency = Mathf.Max(1, source.frequency);
        int startSample = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Max(0f, startSeconds) * frequency),
            0,
            source.samples);
        int remain = source.samples - startSample;
        if (remain <= 0)
            return source;

        int lengthSamples = durationSeconds > 0.001f
            ? Mathf.Min(remain, Mathf.RoundToInt(durationSeconds * frequency))
            : remain;
        if (lengthSamples <= 0 || (startSample == 0 && lengthSamples == source.samples))
            return source;

        var data = new float[lengthSamples * channels];
        if (!source.GetData(data, startSample))
            return source;

        AudioClip sliced = AudioClip.Create(
            source.name + "_slice",
            lengthSamples,
            channels,
            frequency,
            false);
        sliced.SetData(data, 0);
        return sliced;
    }

    // 以随机音高播放一次性音效。
    void PlayClip(AudioClip clip, float volume)
    {
        if (_audio == null || clip == null)
            return;
        _audio.pitch = Random.Range(0.92f, 1.08f);
        _audio.PlayOneShot(clip, volume);
    }

    // 启动命中顿帧协程，先停掉上一次。
    void BeginStop(float seconds)
    {
        if (_stopRoutine != null)
            StopCoroutine(_stopRoutine);
        _stopRoutine = StartCoroutine(StopRoutine(seconds));
    }

    // 短时间压低 timeScale，再用不受缩放的时间恢复。
    IEnumerator StopRoutine(float seconds)
    {
        if (Time.timeScale > 0.2f)
            _savedTimeScale = Time.timeScale;
        Time.timeScale = 0.12f;
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until)
            yield return null;
        Time.timeScale = _savedTimeScale < 0.2f ? 1f : _savedTimeScale;
        _stopRoutine = null;
    }

    // 让跟随相机沿出拳反方向短暂踢一下。
    static void KickCamera(Vector3 forward, float meters)
    {
        HeroFollowCamera cam = Camera.main != null
            ? Camera.main.GetComponent<HeroFollowCamera>()
            : FindAnyObjectByType<HeroFollowCamera>();
        if (cam == null)
            return;
        cam.Kick(-forward, meters);
    }

    // 用白色 PropertyBlock 让受击网格短暂闪白。
    void Flash(GameObject target, float duration)
    {
        if (target == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        float until = Time.unscaledTime + duration;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.white);
            block.SetColor("_Color", Color.white);
            block.SetColor("_EmissionColor", Color.white * 1.6f);
            renderer.SetPropertyBlock(block);
            _flashes.Add(new FlashState { Renderer = renderer, Until = until });
        }
    }

    // 到期后清掉闪白 PropertyBlock。
    void LateUpdate()
    {
        if (_flashes.Count == 0)
            return;

        float now = Time.unscaledTime;
        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            if (now < _flashes[i].Until)
                continue;
            if (_flashes[i].Renderer != null)
                _flashes[i].Renderer.SetPropertyBlock(null);
            _flashes.RemoveAt(i);
        }
    }

    // 生成 Casual Hit 预制体，编辑器外则用粒子兜底。播完即销毁。
    void SpawnImpact(Vector3 position, float scale, int stage)
    {
        GameObject prefab = LoadCasualPrefab(stage);
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * (0.45f + stage * 0.12f);
            Destroy(instance, 1.6f);
            return;
        }

        SpawnFallbackBurst(position, scale);
    }

    // 仅在编辑器中按连招段加载 Casual Hit 预制体。
    static GameObject LoadCasualPrefab(int stage)
    {
#if UNITY_EDITOR
        int index = Mathf.Clamp(stage, 0, CasualPrefabs.Length - 1);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CasualPrefabs[index]);
#else
        return null;
#endif
    }

    // 无预制体时生成一簇短促打击粒子。
    static void SpawnFallbackBurst(Vector3 position, float scale)
    {
        var go = new GameObject("HitImpact");
        go.transform.position = position;
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = 0.18f;
        main.startSpeed = 4.5f * scale;
        main.startSize = 0.12f * scale;
        main.startColor = new Color(1f, 0.86f, 0.35f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 28;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f * scale;

        particles.Play(true);
        Destroy(go, 0.8f);
    }

    // 程序化生成带包络的噪声音效片段。
    static AudioClip MakeNoiseClip(string name, float duration, float toneHz, float volume, bool sweepUp)
    {
        int rate = 22050;
        int samples = Mathf.Max(64, Mathf.RoundToInt(duration * rate));
        var data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float env = t < 0.08f ? t / 0.08f : 1f - (t - 0.08f) / 0.92f;
            env = Mathf.Clamp01(env);
            float hz = sweepUp ? Mathf.Lerp(toneHz, toneHz * 2.4f, t) : Mathf.Lerp(toneHz * 1.6f, toneHz * 0.35f, t);
            phase += (hz / rate) * Mathf.PI * 2f;
            float noise = Random.Range(-1f, 1f);
            data[i] = (Mathf.Sin(phase) * 0.55f + noise * 0.45f) * env * volume;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 销毁时恢复 timeScale 并清空单例引用。
    void OnDestroy()
    {
        if (_stopRoutine != null && Time.timeScale < 0.2f)
            Time.timeScale = 1f;
        if (_instance == this)
            _instance = null;
    }
}
