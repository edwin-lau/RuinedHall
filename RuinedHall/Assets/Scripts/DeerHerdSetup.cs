using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 布置母鹿与雄鹿：配置动作、先藏雄鹿，母鹿半血时再放出加入战斗。
/// </summary>
public static class DeerHerdSetup
{
    static GameObject _stag;
    static bool _stagReleased;

    /// <summary>找到母鹿/雄鹿，挂动作与战斗，并先关掉雄鹿。</summary>
    public static void Ensure()
    {
        GameObject doe = FindByTokens("deer-female") ??
                         FindByTokens("female") ??
                         GameObject.Find("deer-female-mesh");
        GameObject stag = FindByTokens(true, "deer1_textured") ??
                          FindByTokens(true, "deer1");
        _stag = stag;
        _stagReleased = false;

        CharacterActionProfile profile = BuildDeerProfile();
        if (doe != null)
        {
            ConfigureDeer(doe, profile, false);
            if (doe.GetComponent<DoeMateCall>() == null)
                doe.AddComponent<DoeMateCall>();
        }

        if (stag != null)
        {
            ConfigureDeer(stag, profile, true);
            stag.SetActive(false);
        }
    }

    /// <summary>在母鹿附近放出雄鹿并强制追击。</summary>
    public static void ReleaseStag(CharacterCombatAgent doe)
    {
        if (_stagReleased || _stag == null || doe == null)
            return;

        _stagReleased = true;
        Vector3 origin = doe.transform.position;
        Vector3 away = doe.transform.right;

        // 尽量出现在玩家视线侧面，避免和母鹿叠在一起
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero != null)
        {
            Vector3 side = Vector3.Cross(Vector3.up, hero.transform.position - origin);
            if (side.sqrMagnitude > 0.01f)
                away = side.normalized;
        }

        Vector3 spawn = origin + away * 8.5f;
        if (Physics.Raycast(spawn + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 20f))
            spawn = hit.point;

        _stag.SetActive(true);
        var agent = _stag.GetComponent<CharacterCombatAgent>();
        if (agent != null)
        {
            agent.RelocateTo(spawn);
            agent.ForceChase();
        }
        else
        {
            _stag.transform.position = spawn;
        }
    }

    /// <summary>给单只鹿装 Animator、动作播放器、控制器和战斗组件。</summary>
    static void ConfigureDeer(GameObject actor, CharacterActionProfile profile, bool stag)
    {
        if (actor == null)
            return;

        actor.SetActive(true);
        var animator = actor.GetComponent<Animator>();
        if (animator == null)
            animator = actor.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 共用鹿动作配置；雄鹿碰撞体略大
        if (profile != null)
        {
            var player = actor.GetComponent<CharacterActionPlayer>();
            if (player == null)
                player = actor.AddComponent<CharacterActionPlayer>();
            player.SetProfile(profile);
        }

        if (actor.GetComponent<CharacterController>() == null)
        {
            var controller = actor.AddComponent<CharacterController>();
            controller.height = stag ? 1.7f : 1.35f;
            controller.radius = stag ? 0.38f : 0.32f;
            controller.center = new Vector3(0f, stag ? 0.85f : 0.68f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.25f;
            controller.minMoveDistance = 0f;
        }

        if (actor.GetComponent<CharacterCombatAgent>() == null)
            actor.AddComponent<CharacterCombatAgent>();
    }

    /// <summary>从 doe/stag 资源拼出鹿的动作配置。</summary>
    static CharacterActionProfile BuildDeerProfile()
    {
        AnimationClip[] doeClips = Resources.LoadAll<AnimationClip>("characters/others/doe");
        AnimationClip[] stagClips = Resources.LoadAll<AnimationClip>("characters/deer/deer1_textured");
        var actions = new List<CharacterActionDefinition>();
        AddClip(actions, "Idle", FindClip(doeClips, "idle") ?? FindClip(stagClips, "idle"), true);
        AddClip(actions, "Idle2", FindClip(doeClips, "idle 1", "idle.001") ?? FindClip(stagClips, "idle.001"), false);
        AddClip(actions, "Eat", FindClip(stagClips, "eat") ?? FindClip(doeClips, "idle 1", "idle"), false);
        AddClip(actions, "Look", FindClip(stagClips, "lookaround.000", "look") ?? FindClip(doeClips, "idle 1"), false);
        AddClip(actions, "Look2", FindClip(stagClips, "lookaround.001", "look") ?? FindClip(doeClips, "idle"), false);
        AddClip(actions, "Move", FindClip(doeClips, "run", "walk") ?? FindClip(stagClips, "run"), true);
        AddClip(actions, "Jump", FindClip(doeClips, "run") ?? FindClip(stagClips, "run"), false);
        AddClip(actions, "Attack", FindClip(doeClips, "run") ?? FindClip(stagClips, "run"), false, 0.3f);
        AddClip(actions, "Death", FindClip(doeClips, "die", "death") ?? FindClip(stagClips, "die"), false);
        return CreateProfile("Idle", actions);
    }

    /// <summary>按名称关键字找动画片段，跳过预览剪辑。</summary>
    static AnimationClip FindClip(AnimationClip[] clips, params string[] tokens)
    {
        if (clips == null)
            return null;
        foreach (string token in tokens)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    continue;
                if (clip.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }

    /// <summary>有片段才加入动作定义。</summary>
    static void AddClip(
        List<CharacterActionDefinition> actions,
        string id,
        AnimationClip clip,
        bool loop,
        float impactTime = 0.4f)
    {
        if (clip == null)
            return;
        actions.Add(new CharacterActionDefinition(id, clip, loop, 1f, 0.1f, impactTime));
    }

    /// <summary>生成动作配置，默认动作缺失时改用第一条。</summary>
    static CharacterActionProfile CreateProfile(string defaultAction, List<CharacterActionDefinition> actions)
    {
        if (actions.Count == 0)
            return null;
        var profile = ScriptableObject.CreateInstance<CharacterActionProfile>();
        string resolved = actions.Exists(action => action.Id == defaultAction)
            ? defaultAction
            : actions[0].Id;
        profile.Configure(resolved, actions);
        return profile;
    }

    /// <summary>按名字包含的关键字查找激活物体。</summary>
    static GameObject FindByTokens(params string[] tokens)
    {
        return FindByTokens(false, tokens);
    }

    /// <summary>在场景 Transform 中按名字包含全部关键字查找物体。</summary>
    static GameObject FindByTokens(bool includeInactive, params string[] tokens)
    {
        var transforms = includeInactive
            ? Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            : Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        foreach (Transform transform in transforms)
        {
            if (transform == null)
                continue;
            string name = transform.gameObject.name;
            bool match = true;
            foreach (string token in tokens)
            {
                if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return transform.gameObject;
        }

        return null;
    }
}
