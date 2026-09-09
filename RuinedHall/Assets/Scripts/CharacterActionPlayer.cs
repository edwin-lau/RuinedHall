using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 用 PlayableGraph 播放角色动作配置：交叉淡入、特效 cue、暂停与循环。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class CharacterActionPlayer : MonoBehaviour
{
    [SerializeField] CharacterActionProfile profile;
    [SerializeField] bool playDefaultOnEnable = true;
    [SerializeField] float playbackSpeed = 1f;

    readonly List<ActiveEffect> _activeEffects = new();

    Animator _animator;
    PlayableGraph _graph;
    AnimationMixerPlayable _mixer;
    AnimationClipPlayable _currentPlayable;
    CharacterActionDefinition _currentAction;
    bool[] _firedCues = Array.Empty<bool>();
    int _currentInput = -1;
    int _previousInput = -1;
    float _fadeDuration;
    float _fadeElapsed;
    float _actionElapsed;
    bool _hasCurrentPlayable;
    bool _isFading;
    bool _completed;
    bool _paused;

    public event Action<string> ActionStarted;
    public event Action<string> ActionCompleted;

    /// <summary>当前动作配置。</summary>
    public CharacterActionProfile Profile => profile;
    /// <summary>正在播放的动作定义。</summary>
    public CharacterActionDefinition CurrentAction => _currentAction;
    /// <summary>当前动作 Id。</summary>
    public string CurrentActionId => _currentAction?.Id;
    /// <summary>是否处于暂停。</summary>
    public bool IsPaused => _paused;
    /// <summary>当前动作已播放秒数。</summary>
    public float ActionElapsed => _actionElapsed;
    /// <summary>当前动作按时速算出的时长。</summary>
    public float PlaybackLength => AnimPlayback.Length(_currentAction, playbackSpeed);
    /// <summary>当前片段归一化时间。</summary>
    public float NormalizedTime
    {
        get
        {
            if (!_hasCurrentPlayable || _currentAction?.Clip == null || _currentAction.Clip.length <= 0f)
                return 0f;
            return (float)(_currentPlayable.GetTime() / _currentAction.Clip.length);
        }
    }

    /// <summary>非循环动作是否已播完。</summary>
    public bool PlaybackFinished
    {
        get
        {
            if (!_hasCurrentPlayable || _currentAction == null)
                return true;
            if (_currentAction.Loop)
                return false;
            return NormalizedTime >= 1f;
        }
    }

    // 关掉根运动并搭建 PlayableGraph。
    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        InitializeGraph();
    }

    // 启用时播放图，并按需切入默认动作。
    void OnEnable()
    {
        if (_graph.IsValid())
            _graph.Play();

        if (playDefaultOnEnable && _currentAction == null && profile != null)
            Play(profile.DefaultAction, true);
    }

    // 推进交叉淡入、当前动作计时与特效寿命。
    void Update()
    {
        if (_paused)
            return;

        float deltaTime = Time.deltaTime * Mathf.Max(0f, playbackSpeed);
        UpdateCrossFade(deltaTime);
        UpdateCurrentAction(deltaTime);
        UpdateEffects(deltaTime);
    }

    // 停掉 PlayableGraph。
    void OnDisable()
    {
        if (_graph.IsValid())
            _graph.Stop();
    }

    // 清特效并销毁图。
    void OnDestroy()
    {
        ClearEffects();
        if (_graph.IsValid())
            _graph.Destroy();
    }

    /// <summary>切换动作配置，可选立即播放默认动作。</summary>
    public void SetProfile(CharacterActionProfile newProfile, bool playDefault = true)
    {
        profile = newProfile;
        if (playDefault && isActiveAndEnabled && profile != null)
            Play(profile.DefaultAction, true);
    }

    /// <summary>播放指定动作，可交叉淡入并触发特效 cue。</summary>
    public bool Play(string actionId, bool restart = false)
    {
        if (profile == null || !profile.TryGet(actionId, out CharacterActionDefinition action))
        {
            Debug.LogWarning(
                $"CharacterActionPlayer '{name}' has no action '{actionId}'.",
                this);
            return false;
        }

        if (action.Clip == null)
        {
            Debug.LogWarning(
                $"CharacterActionPlayer '{name}' action '{actionId}' has no clip.",
                this);
            return false;
        }

        if (!restart &&
            _currentAction != null &&
            string.Equals(_currentAction.Id, action.Id, StringComparison.OrdinalIgnoreCase))
        {
            // 同一动作且不强制重播，直接视为成功。
            return true;
        }

        if (!_graph.IsValid())
            InitializeGraph();

        // 双缓冲 mixer：切到另一路输入，避免打断当前片段。
        int nextInput = _currentInput < 0 ? 0 : 1 - _currentInput;
        DisconnectInput(nextInput);

        AnimationClipPlayable nextPlayable =
            AnimationClipPlayable.Create(_graph, action.Clip);
        nextPlayable.SetApplyFootIK(false);
        nextPlayable.SetApplyPlayableIK(false);
        nextPlayable.SetSpeed(action.Speed);
        nextPlayable.SetTime(0d);
        _graph.Connect(nextPlayable, 0, _mixer, nextInput);

        _previousInput = _currentInput;
        _currentInput = nextInput;
        _currentPlayable = nextPlayable;
        _hasCurrentPlayable = true;
        _currentAction = action;
        _actionElapsed = 0f;
        _completed = false;
        int cueCount = action.Effects.Count;
        if (_firedCues.Length != cueCount)
            _firedCues = cueCount > 0 ? new bool[cueCount] : Array.Empty<bool>();
        else if (cueCount > 0)
            Array.Clear(_firedCues, 0, cueCount);

        // 有上一段动作才交叉淡入，否则立刻切满权重。
        _fadeDuration = _previousInput < 0 ? 0f : action.CrossFade;
        _fadeElapsed = 0f;
        _isFading = _fadeDuration > 0f;
        _mixer.SetInputWeight(_currentInput, _isFading ? 0f : 1f);
        if (_previousInput >= 0)
            _mixer.SetInputWeight(_previousInput, _isFading ? 1f : 0f);
        if (!_isFading && _previousInput >= 0)
            DisconnectInput(_previousInput);

        Resume();
        ActionStarted?.Invoke(action.Id);
        FireDueCues();
        return true;
    }

    /// <summary>暂停动画与粒子特效。</summary>
    public void Pause()
    {
        if (_paused)
            return;

        _paused = true;
        if (_mixer.IsValid())
            _mixer.SetSpeed(0d);
        SetParticlePause(true);
    }

    /// <summary>恢复动画与粒子特效。</summary>
    public void Resume()
    {
        _paused = false;
        if (_mixer.IsValid())
            _mixer.SetSpeed(Mathf.Max(0f, playbackSpeed));
        SetParticlePause(false);
    }

    /// <summary>停止当前动作并清掉 mixer 输入与特效。</summary>
    public void Stop()
    {
        _currentAction = null;
        _hasCurrentPlayable = false;
        _currentInput = -1;
        _previousInput = -1;
        _isFading = false;
        DisconnectInput(0);
        DisconnectInput(1);
        ClearEffects();
    }

    /// <summary>设置播放倍速。</summary>
    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Max(0f, speed);
        if (!_paused && _mixer.IsValid())
            _mixer.SetSpeed(playbackSpeed);
    }

    /// <summary>从配置中查找动作定义。</summary>
    public bool TryGetAction(
        string actionId,
        out CharacterActionDefinition action)
    {
        action = null;
        return profile != null && profile.TryGet(actionId, out action);
    }

    // 创建双输入 AnimationMixer 并接到 Animator。
    void InitializeGraph()
    {
        if (_graph.IsValid())
            return;

        _graph = PlayableGraph.Create($"{name}.CharacterActions");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _mixer = AnimationMixerPlayable.Create(_graph, 2);
        AnimationPlayableOutput output =
            AnimationPlayableOutput.Create(_graph, "Character Animation", _animator);
        output.SetSourcePlayable(_mixer);
        _mixer.SetSpeed(Mathf.Max(0f, playbackSpeed));
        _graph.Play();
    }

    // 在新旧片段之间插值 mixer 权重，淡完断开旧输入。
    void UpdateCrossFade(float deltaTime)
    {
        if (!_isFading)
            return;

        _fadeElapsed += deltaTime;
        float weight = Mathf.Clamp01(_fadeElapsed / Mathf.Max(0.001f, _fadeDuration));
        _mixer.SetInputWeight(_currentInput, weight);
        if (_previousInput >= 0)
            _mixer.SetInputWeight(_previousInput, 1f - weight);

        if (weight < 1f)
            return;

        _isFading = false;
        if (_previousInput >= 0)
            DisconnectInput(_previousInput);
        _previousInput = -1;
    }

    // 推进动作时间、循环取模，并在播完时发出完成事件。
    void UpdateCurrentAction(float deltaTime)
    {
        if (!_hasCurrentPlayable || _currentAction == null)
            return;

        _actionElapsed += deltaTime;
        if (_currentAction.Loop && _currentAction.Clip.length > 0f)
        {
            double clipTime = _currentPlayable.GetTime();
            if (clipTime >= _currentAction.Clip.length)
                _currentPlayable.SetTime(clipTime % _currentAction.Clip.length);
        }

        FireDueCues();
        if (_currentAction.Loop || _completed || NormalizedTime < 1f)
            return;

        _completed = true;
        ActionCompleted?.Invoke(_currentAction.Id);
    }

    // 到点触发尚未发射的特效 cue。
    void FireDueCues()
    {
        if (_currentAction == null)
            return;

        for (int i = 0; i < _currentAction.Effects.Count; i++)
        {
            if (_firedCues[i])
                continue;

            CharacterEffectCue cue = _currentAction.Effects[i];
            if (cue == null)
                continue;
            double clipTime = _hasCurrentPlayable ? _currentPlayable.GetTime() : _actionElapsed;
            if (clipTime < cue.TriggerTime)
                continue;

            _firedCues[i] = true;
            SpawnEffect(cue);
        }
    }

    // 在挂点生成预制体或内置粒子特效。
    void SpawnEffect(CharacterEffectCue cue)
    {
        Transform socket = string.IsNullOrWhiteSpace(cue.SocketName)
            ? transform
            : FindChild(transform, cue.SocketName) ?? transform;

        Quaternion localRotation = Quaternion.Euler(cue.LocalEulerAngles);
        GameObject instance;
        Material ownedMaterial = null;
        if (cue.Kind == CharacterEffectKind.Prefab && cue.Prefab != null)
        {
            // 预制体可跟随挂点，或生成在世界空间一次性位置。
            if (cue.FollowSocket)
            {
                instance = Instantiate(cue.Prefab, socket);
                instance.transform.localPosition = cue.LocalPosition;
                instance.transform.localRotation = localRotation;
            }
            else
            {
                instance = Instantiate(
                    cue.Prefab,
                    socket.TransformPoint(cue.LocalPosition),
                    socket.rotation * localRotation);
            }
        }
        else
        {
            instance = CreateBuiltInEffect(cue, socket, out ownedMaterial);
        }

        _activeEffects.Add(new ActiveEffect(instance, ownedMaterial, cue.Lifetime));
    }

    // 按 cue 类型搭一簇尘土或火花粒子。
    static GameObject CreateBuiltInEffect(
        CharacterEffectCue cue,
        Transform socket,
        out Material ownedMaterial)
    {
        var effect = new GameObject($"CharacterFX_{cue.Kind}");
        if (cue.FollowSocket)
        {
            effect.transform.SetParent(socket, false);
            effect.transform.localPosition = cue.LocalPosition;
            effect.transform.localRotation = Quaternion.Euler(cue.LocalEulerAngles);
        }
        else
        {
            effect.transform.SetPositionAndRotation(
                socket.TransformPoint(cue.LocalPosition),
                socket.rotation * Quaternion.Euler(cue.LocalEulerAngles));
        }

        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 尘土用圆面慢粒子，其它用小球爆发。
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = cue.Kind == CharacterEffectKind.DustBurst ? 0.65f : 0.3f;
        main.startSpeed = cue.Kind == CharacterEffectKind.DustBurst ? 1.5f : 3.5f;
        main.startSize = cue.Size * (cue.Kind == CharacterEffectKind.DustBurst ? 0.35f : 0.16f);
        main.startColor = cue.Color;
        main.simulationSpace = cue.FollowSocket
            ? ParticleSystemSimulationSpace.Local
            : ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(
                0f,
                (short)(cue.Kind == CharacterEffectKind.DustBurst ? 18 : 10))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = cue.Kind == CharacterEffectKind.DustBurst
            ? ParticleSystemShapeType.Circle
            : ParticleSystemShapeType.Sphere;
        shape.radius = cue.Size * 0.28f;

        ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        ownedMaterial = shader == null ? null : new Material(shader);
        if (ownedMaterial != null)
            renderer.material = ownedMaterial;

        particles.Play();
        return effect;
    }

    // 到期销毁仍在播的特效实例。
    void UpdateEffects(float deltaTime)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _activeEffects[i];
            effect.Remaining -= deltaTime;
            if (effect.Remaining > 0f && effect.Instance != null)
                continue;

            effect.Destroy();
            _activeEffects.RemoveAt(i);
        }
    }

    // 暂停或恢复所有活动粒子。
    void SetParticlePause(bool pause)
    {
        foreach (ActiveEffect effect in _activeEffects)
        {
            if (effect.Instance == null)
                continue;

            foreach (ParticleSystem particles in
                     effect.Instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (pause)
                    particles.Pause(true);
                else
                    particles.Play(true);
            }
        }
    }

    // 立刻销毁全部活动特效。
    void ClearEffects()
    {
        foreach (ActiveEffect effect in _activeEffects)
            effect.Destroy();
        _activeEffects.Clear();
    }

    // 断开 mixer 某一路输入并销毁对应 playable。
    void DisconnectInput(int input)
    {
        if (!_mixer.IsValid() || input < 0 || input >= _mixer.GetInputCount())
            return;

        Playable playable = _mixer.GetInput(input);
        if (!playable.IsValid())
            return;

        _graph.Disconnect(_mixer, input);
        playable.Destroy();
        _mixer.SetInputWeight(input, 0f);
    }

    // 按名称递归查找子节点。
    static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChild(child, childName);
            if (nested != null)
                return nested;
        }
        return null;
    }

    /// <summary>跟踪一个已生成特效及其可选自有材质。</summary>
    sealed class ActiveEffect
    {
        public readonly GameObject Instance;
        readonly Material _ownedMaterial;
        public float Remaining;

        /// <summary>记录实例、自有材质与剩余寿命。</summary>
        public ActiveEffect(GameObject instance, Material ownedMaterial, float lifetime)
        {
            Instance = instance;
            _ownedMaterial = ownedMaterial;
            Remaining = lifetime;
        }

        /// <summary>销毁特效物体与自有材质。</summary>
        public void Destroy()
        {
            if (Instance != null)
                UnityEngine.Object.Destroy(Instance);
            if (_ownedMaterial != null)
                UnityEngine.Object.Destroy(_ownedMaterial);
        }
    }
}
