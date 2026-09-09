using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 给场景里的 Skeleton Rogue 补动作表、碰撞和战斗组件。
/// </summary>
public static class SkeletonRogueSetup
{
    const string GeneralPath = "characters/Skeleton/Animations/fbx/Rig_Medium/Rig_Medium_General";
    const string MovePath = "characters/Skeleton/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic";

    /// <summary>按名字找到 Rogue，挂上 Animator / ActionPlayer / Combat。</summary>
    public static void Ensure()
    {
        GameObject actor = GameObject.Find("Skeleton_Rogue (1)") ?? GameObject.Find("Skeleton_Rogue");
        if (actor == null)
            return;

        CharacterActionProfile profile = BuildProfile();
        if (profile == null)
            return;

        var animator = actor.GetComponent<Animator>();
        if (animator == null)
            animator = actor.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var player = actor.GetComponent<CharacterActionPlayer>();
        if (player == null)
            player = actor.AddComponent<CharacterActionPlayer>();
        player.SetProfile(profile);

        if (actor.GetComponent<CharacterController>() == null)
        {
            var controller = actor.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.88f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.28f;
            controller.minMoveDistance = 0f;
        }

        if (actor.GetComponent<CharacterCombatAgent>() == null)
            actor.AddComponent<CharacterCombatAgent>();
    }

    /// <summary>从 Resources 动画拼一份动作表；失败则退回已有 asset。</summary>
    static CharacterActionProfile BuildProfile()
    {
        var existing = Resources.Load<CharacterActionProfile>("characters/Profiles/SkeletonRogueActions");
        AnimationClip[] clips = LoadClips();
        if (clips.Length == 0)
            return existing;

        var actions = new List<CharacterActionDefinition>();
        Add(actions, "Idle", Find(clips, "idle_a", "idle"), true);
        Add(actions, "Idle2", Find(clips, "idle_b"), true);
        Add(actions, "Move", Find(clips, "running_a", "running", "walking_a"), true);
        Add(actions, "Hit", Find(clips, "hit_a", "hit"), false);
        Add(actions, "Death", Find(clips, "death_a", "death"), false);
        Add(actions, "Spawn", Find(clips, "spawn_ground", "spawnground"), false, 0.2f);
        Add(actions, "Throw", Find(clips, "throw"), false, 0.42f);
        Add(actions, "Attack", Find(clips, "throw"), false, 0.42f);
        if (actions.Count == 0)
            return existing;

        var profile = ScriptableObject.CreateInstance<CharacterActionProfile>();
        profile.Configure("Idle", actions);
        return profile;
    }

    /// <summary>加载 idle/move 两类 FBX 里的 clip。</summary>
    static AnimationClip[] LoadClips()
    {
        var list = new List<AnimationClip>();
        list.AddRange(Resources.LoadAll<AnimationClip>(GeneralPath));
        list.AddRange(Resources.LoadAll<AnimationClip>(MovePath));
        return list.ToArray();
    }

    /// <summary>按名字关键字找 clip，忽略预览片段。</summary>
    static AnimationClip Find(AnimationClip[] clips, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    continue;
                string compact = clip.name.Replace(" ", "").Replace("|", "_");
                if (compact.IndexOf(token.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }

    /// <summary>有 clip 才加入动作表。</summary>
    static void Add(
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
}
