using UnityEngine;

/// <summary>
/// 玩家走跑表现：动画速率与位移匹配、左右脚交替脚步音与尘土粒子（iOS 预算友好）。
/// </summary>
[RequireComponent(typeof(HeroController))]
[RequireComponent(typeof(Animator))]
public sealed class HeroLocomotionFeel : MonoBehaviour
{
    [Header("动画匹配（1.8m 参考身高下的自然走跑速度）")]
    [SerializeField] float walkAnimReferenceSpeed = 1.48f;
    [SerializeField] float runAnimReferenceSpeed = 3.55f;
    [SerializeField] float minAnimSpeed = 0.7f;
    [SerializeField] float maxAnimSpeed = 1.08f;

    [Header("脚步触地相位（归一化循环 0–1）")]
    [SerializeField] float[] walkFootContacts = { 0.1f, 0.6f };
    [SerializeField] float[] runFootContacts = { 0.08f, 0.54f };

    [Header("脚步反馈")]
    [SerializeField] float walkFootVolume = 0.28f;
    [SerializeField] float runFootVolume = 0.76f;
    [SerializeField] float minSpeedForFootsteps = 0.35f;
    [SerializeField] int maxActiveDustBursts = 6;
    [SerializeField] int walkDustCount = 8;
    [SerializeField] int runDustCount = 12;

    const string WalkClipPath = "characters/hero2/audio/O_walk";
    const string RunClipPath = "characters/hero2/audio/O_running";
    const float RunLoopPitch = 2f;
    const float LoopIntroSkip = 0.42f;
    const float LoopStopGrace = 0.16f;

    static readonly Color DustColor = new(0.58f, 0.48f, 0.34f, 0.72f);

    HeroController _hero;
    Animator _animator;
    CharacterController _controller;
    AudioSource _audio;
    AudioClip _footstepClip;
    AudioClip _walkLoop;
    AudioClip _runLoop;
    Material _dustMaterial;

    float _prevPhase;
    bool _leftFootNext;
    int _activeDustBursts;
    float _defaultAnimSpeed = 1f;
    AudioClip _activeLoop;
    float _activeLoopPitch;
    float _stopLoopAt = -1f;

    void Awake()
    {
        _hero = GetComponent<HeroController>();
        _animator = GetComponent<Animator>();
        _controller = GetComponent<CharacterController>();
        _defaultAnimSpeed = _animator != null ? _animator.speed : 1f;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.ignoreListenerPause = true;
        _audio.minDistance = 0.8f;
        _audio.maxDistance = 14f;
        _audio.rolloffMode = AudioRolloffMode.Linear;
        _walkLoop = PrepareLoop(Resources.Load<AudioClip>(WalkClipPath));
        _runLoop = PrepareLoop(Resources.Load<AudioClip>(RunClipPath));
        _footstepClip = _walkLoop == null && _runLoop == null
            ? BuildFootstepClip()
            : null;
    }

    void Update()
    {
        if (_hero == null || _animator == null || _controller == null)
            return;
        if (_hero.IsDead)
            return;

        UpdateLocomotionLoop();
        UpdateFootsteps();
    }

    void LateUpdate()
    {
        if (_hero == null || _animator == null)
            return;

        if (!_hero.AllowsLocomotionAnimSpeed)
        {
            if (!_hero.OverridesAnimSpeed &&
                !Mathf.Approximately(_animator.speed, _defaultAnimSpeed))
                _animator.speed = _defaultAnimSpeed;
            return;
        }

        float scale = _hero.MotionScale;
        float reference = _hero.IsRunning
            ? runAnimReferenceSpeed * scale
            : walkAnimReferenceSpeed * scale;
        float speed = Mathf.Max(0f, _hero.PlanarSpeed);
        if (reference <= 0.01f || speed < reference * 0.12f)
        {
            _animator.speed = _defaultAnimSpeed;
            return;
        }

        _animator.speed = Mathf.Clamp(
            speed / reference,
            minAnimSpeed,
            maxAnimSpeed);
    }

    // 走路循环 O_walk；跑步循环 O_running 并 2 倍速。已去掉片头冲击，避免反复听到开头。
    void UpdateLocomotionLoop()
    {
        if (_audio == null)
            return;

        float scale = _hero.MotionScale;
        bool moving = _controller.isGrounded &&
            _hero.IsInLocomotion &&
            _hero.PlanarSpeed >= minSpeedForFootsteps * scale;
        if (!moving)
        {
            if (_stopLoopAt < 0f)
                _stopLoopAt = Time.unscaledTime + LoopStopGrace;
            if (Time.unscaledTime >= _stopLoopAt)
                StopLocomotionLoop();
            return;
        }

        _stopLoopAt = -1f;
        if (_hero.IsRunning)
            PlayLocomotionLoop(_runLoop, RunLoopPitch, runFootVolume);
        else
            PlayLocomotionLoop(_walkLoop, 1f, walkFootVolume);
    }

    void PlayLocomotionLoop(AudioClip clip, float pitch, float volume)
    {
        if (clip == null)
        {
            StopLocomotionLoop();
            return;
        }

        if (_activeLoop == clip && _audio.clip == clip && _audio.loop)
        {
            _audio.volume = volume;
            if (Mathf.Abs(_activeLoopPitch - pitch) > 0.01f)
            {
                _activeLoopPitch = pitch;
                _audio.pitch = pitch;
            }

            if (!_audio.isPlaying)
                _audio.UnPause();
            return;
        }

        _audio.Stop();
        _audio.clip = clip;
        _audio.loop = true;
        _audio.pitch = pitch;
        _audio.volume = volume;
        float span = Mathf.Max(0.05f, clip.length * 0.72f);
        _audio.time = Random.Range(0f, span);
        _audio.Play();
        _activeLoop = clip;
        _activeLoopPitch = pitch;
    }

    void StopLocomotionLoop()
    {
        _stopLoopAt = -1f;
        _activeLoop = null;
        _activeLoopPitch = 1f;
        if (_audio == null)
            return;
        if (_audio.isPlaying)
            _audio.Stop();
        _audio.loop = false;
        _audio.clip = null;
        _audio.pitch = 1f;
        _audio.time = 0f;
    }

    static AudioClip PrepareLoop(AudioClip source)
    {
        if (source == null || source.length <= LoopIntroSkip + 0.6f)
            return source;
        float duration = source.length - LoopIntroSkip - 0.12f;
        return SliceClip(source, LoopIntroSkip, duration) ?? source;
    }

    static AudioClip SliceClip(AudioClip source, float startSeconds, float durationSeconds)
    {
        if (source == null || source.samples <= 0)
            return source;
        if (!source.LoadAudioData())
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
            source.name + "_loop",
            lengthSamples,
            channels,
            frequency,
            false);
        sliced.SetData(data, 0);
        return sliced;
    }

    void UpdateFootsteps()
    {
        if (!_controller.isGrounded || !_hero.IsInLocomotion)
        {
            _prevPhase = ReadLocomotionPhase();
            return;
        }

        float speed = _hero.PlanarSpeed;
        float scale = _hero.MotionScale;
        if (speed < minSpeedForFootsteps * scale)
        {
            _prevPhase = ReadLocomotionPhase();
            return;
        }

        float phase = ReadLocomotionPhase();
        float[] contacts = _hero.IsRunning ? runFootContacts : walkFootContacts;
        if (contacts == null || contacts.Length == 0)
        {
            _prevPhase = phase;
            return;
        }

        for (int i = 0; i < contacts.Length; i++)
        {
            if (!CrossedPhase(_prevPhase, phase, contacts[i]))
                continue;
            TriggerFootstep(_hero.IsRunning);
        }

        _prevPhase = phase;
    }

    float ReadLocomotionPhase()
    {
        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        float phase = info.normalizedTime;
        return phase - Mathf.Floor(phase);
    }

    static bool CrossedPhase(float previous, float current, float threshold)
    {
        if (previous < current)
            return previous < threshold && current >= threshold;

        // 循环回绕时只触发靠近 0 的触地点，避免误触后半段相位。
        if (previous <= 0.82f)
            return false;
        return threshold <= Mathf.Max(current, 0.02f) + 0.05f;
    }

    void TriggerFootstep(bool running)
    {
        bool left = _leftFootNext;
        _leftFootNext = !_leftFootNext;

        Vector3 position = FootWorldPosition(left);
        PlayFootSound(running, left);
        SpawnDust(position, running);
    }

    Vector3 FootWorldPosition(bool left)
    {
        if (_animator != null && _animator.isHuman)
        {
            Transform foot = _animator.GetBoneTransform(
                left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            if (foot != null)
                return foot.position;
        }

        Vector3 side = transform.right * (left ? -0.12f : 0.12f);
        return transform.position + side + Vector3.up * 0.05f;
    }

    void PlayFootSound(bool running, bool left)
    {
        if (_walkLoop != null || _runLoop != null)
            return;
        if (_audio == null || _footstepClip == null)
            return;

        float volume = running ? runFootVolume : walkFootVolume;
        _audio.pitch = left
            ? Random.Range(0.9f, 0.98f)
            : Random.Range(1.02f, 1.1f);
        _audio.PlayOneShot(_footstepClip, volume);
    }

    void SpawnDust(Vector3 position, bool running)
    {
        if (_activeDustBursts >= maxActiveDustBursts)
            return;

        int count = running ? runDustCount : walkDustCount;
        var root = new GameObject("FootDust");
        root.transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.18f;
        main.loop = false;
        main.startLifetime = running ? 0.42f : 0.55f;
        main.startSpeed = running ? 1.1f : 0.75f;
        main.startSize = running ? 0.22f : 0.18f;
        main.startColor = DustColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 2;
        main.gravityModifier = 0.2f;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.06f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        Material material = GetDustMaterial();
        if (material != null)
            renderer.material = material;

        particles.Play(true);
        _activeDustBursts++;
        root.AddComponent<FootDustTracker>().Begin(0.75f, ReleaseDust);
        Destroy(root, 0.85f);
    }

    void ReleaseDust()
    {
        _activeDustBursts = Mathf.Max(0, _activeDustBursts - 1);
    }

    Material GetDustMaterial()
    {
        if (_dustMaterial != null)
            return _dustMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        _dustMaterial = new Material(shader);
        return _dustMaterial;
    }

    static AudioClip BuildFootstepClip()
    {
        const int rate = 22050;
        const float duration = 0.07f;
        int samples = Mathf.Max(32, Mathf.RoundToInt(duration * rate));
        var data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float attack = Mathf.Clamp01(t / 0.12f);
            float decay = 1f - Mathf.Clamp01((t - 0.12f) / 0.88f);
            float env = attack * decay;
            float hz = Mathf.Lerp(180f, 70f, t);
            phase += (hz / rate) * Mathf.PI * 2f;
            float thump = Mathf.Sin(phase) * 0.55f;
            float grit = (Random.value * 2f - 1f) * 0.45f;
            data[i] = (thump + grit) * env * 0.42f;
        }

        AudioClip clip = AudioClip.Create("Footstep", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        StopLocomotionLoop();
        if (_dustMaterial != null)
            Destroy(_dustMaterial);
    }

    sealed class FootDustTracker : MonoBehaviour
    {
        float _until;
        System.Action _onDone;

        public void Begin(float seconds, System.Action onDone)
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
