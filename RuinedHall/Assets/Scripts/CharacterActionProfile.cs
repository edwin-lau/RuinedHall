using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterEffectKind
{
    Prefab,
    HitSpark,
    DustBurst
}

[Serializable]
public sealed class CharacterEffectCue
{
    [SerializeField] CharacterEffectKind kind;
    [SerializeField] GameObject prefab;
    [SerializeField] float triggerTime;
    [SerializeField] float lifetime = 1f;
    [SerializeField] string socketName;
    [SerializeField] Vector3 localPosition;
    [SerializeField] Vector3 localEulerAngles;
    [SerializeField] bool followSocket;
    [SerializeField] Color color = Color.white;
    [SerializeField] float size = 1f;

    public CharacterEffectKind Kind => kind;
    public GameObject Prefab => prefab;
    public float TriggerTime => Mathf.Max(0f, triggerTime);
    public float Lifetime => Mathf.Max(0.05f, lifetime);
    public string SocketName => socketName;
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public bool FollowSocket => followSocket;
    public Color Color => color;
    public float Size => Mathf.Max(0.01f, size);

    public CharacterEffectCue(
        CharacterEffectKind kind,
        float triggerTime,
        float lifetime,
        Color color,
        float size,
        string socketName = "",
        Vector3 localPosition = default,
        bool followSocket = false,
        GameObject prefab = null)
    {
        this.kind = kind;
        this.triggerTime = triggerTime;
        this.lifetime = lifetime;
        this.color = color;
        this.size = size;
        this.socketName = socketName;
        this.localPosition = localPosition;
        this.followSocket = followSocket;
        this.prefab = prefab;
    }
}

[Serializable]
public sealed class CharacterActionDefinition
{
    [SerializeField] string id;
    [SerializeField] AnimationClip clip;
    [SerializeField] bool loop;
    [SerializeField] float speed = 1f;
    [SerializeField] float crossFade = 0.12f;
    [SerializeField] float impactTime = 0.4f;
    [SerializeField] CharacterEffectCue[] effects = Array.Empty<CharacterEffectCue>();

    public string Id => id;
    public AnimationClip Clip => clip;
    public bool Loop => loop;
    public float Speed => Mathf.Max(0.01f, speed);
    public float CrossFade => Mathf.Max(0f, crossFade);
    public float ImpactTime => Mathf.Max(0f, impactTime);
    public IReadOnlyList<CharacterEffectCue> Effects => effects;
    public float Duration => AnimPlayback.Length(clip, Speed);

    public CharacterActionDefinition(
        string id,
        AnimationClip clip,
        bool loop,
        float speed = 1f,
        float crossFade = 0.12f,
        float impactTime = 0.4f,
        params CharacterEffectCue[] effects)
    {
        this.id = id;
        this.clip = clip;
        this.loop = loop;
        this.speed = speed;
        this.crossFade = crossFade;
        this.impactTime = impactTime;
        this.effects = effects ?? Array.Empty<CharacterEffectCue>();
    }
}

[CreateAssetMenu(
    fileName = "CharacterActionProfile",
    menuName = "Ruined Hall/Character Action Profile")]
public sealed class CharacterActionProfile : ScriptableObject
{
    [SerializeField] string defaultAction = "Idle";
    [SerializeField] List<CharacterActionDefinition> actions = new();

    readonly Dictionary<string, CharacterActionDefinition> _lookup =
        new(StringComparer.OrdinalIgnoreCase);

    public string DefaultAction => defaultAction;
    public IReadOnlyList<CharacterActionDefinition> Actions => actions;

    void OnEnable()
    {
        RebuildLookup();
    }

    public bool TryGet(string actionId, out CharacterActionDefinition action)
    {
        if (_lookup.Count != actions.Count)
            RebuildLookup();
        return _lookup.TryGetValue(actionId ?? string.Empty, out action);
    }

    public void Configure(
        string newDefaultAction,
        IEnumerable<CharacterActionDefinition> newActions)
    {
        defaultAction = newDefaultAction;
        actions = newActions == null
            ? new List<CharacterActionDefinition>()
            : new List<CharacterActionDefinition>(newActions);
        RebuildLookup();
    }

    void RebuildLookup()
    {
        _lookup.Clear();
        foreach (CharacterActionDefinition action in actions)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Id))
                continue;
            _lookup[action.Id] = action;
        }
    }
}
