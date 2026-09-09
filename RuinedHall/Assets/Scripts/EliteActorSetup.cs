using UnityEngine;

/// <summary>
/// 给场景里的精英角色（盗贼、雷神木桩等）补齐动作播放器和战斗组件。
/// </summary>
public static class EliteActorSetup
{
    /// <summary>确保命名精英都挂上战斗所需组件。</summary>
    public static void EnsureNamedElites()
    {
        SkeletonRogueSetup.Ensure();
        TryEnsure("Rogue");
        EnsureThorDummy();
    }

    /// <summary>给雷神木桩补动作播放器和战斗代理。</summary>
    static void EnsureThorDummy()
    {
        GameObject actor = GameObject.Find("thor-god") ?? GameObject.Find("thor");
        if (actor == null)
            return;

        if (actor.GetComponent<CharacterCombatAgent>() == null)
        {
            // 没有动作播放器时，用 Animator 片段临时拼一份配置
            if (actor.GetComponent<CharacterActionPlayer>() == null)
            {
                var player = actor.AddComponent<CharacterActionPlayer>();
                CharacterActionProfile profile = BuildProfileFromAnimator(actor);
                if (profile != null)
                    player.SetProfile(profile);
            }

            if (actor.GetComponent<CharacterController>() == null)
                actor.AddComponent<CharacterController>();
            actor.AddComponent<CharacterCombatAgent>();
        }
    }

    /// <summary>按物体名查找角色并装上动作配置与战斗组件。</summary>
    static void TryEnsure(string objectName)
    {
        GameObject actor = GameObject.Find(objectName);
        if (actor == null || actor.GetComponent<CharacterCombatAgent>() != null)
            return;

        // 骷髅走 SkeletonRogue 配置，其余走 Rogue；资源缺失再从 Animator 推断
        CharacterActionProfile profile = Resources.Load<CharacterActionProfile>(
            objectName.IndexOf("Skeleton", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? "characters/Profiles/SkeletonRogueActions"
                : "characters/Profiles/RogueActions");
        if (profile == null)
            profile = BuildProfileFromAnimator(actor);
        if (profile == null)
            return;

        var player = actor.GetComponent<CharacterActionPlayer>();
        if (player == null)
            player = actor.AddComponent<CharacterActionPlayer>();
        player.SetProfile(profile);

        if (actor.GetComponent<CharacterController>() == null)
        {
            var controller = actor.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0f;
        }

        actor.AddComponent<CharacterCombatAgent>();
    }

    /// <summary>从 Animator 片段推断 Idle/移动/攻击等动作配置。</summary>
    static CharacterActionProfile BuildProfileFromAnimator(GameObject actor)
    {
        var animator = actor.GetComponent<Animator>();
        AnimationClip[] clips = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips
            : null;
        if (clips == null || clips.Length == 0)
            return null;

        // 按关键字挑常用动作；没有 Idle 就退回第一条片段
        AnimationClip idle = FindClip(clips, "idle", "stand");
        AnimationClip move = FindClip(clips, "walk", "run", "move");
        AnimationClip attack = FindClip(clips, "attack", "slash", "melee");
        AnimationClip hit = FindClip(clips, "hit", "hurt", "react");
        AnimationClip death = FindClip(clips, "death", "die");
        if (idle == null)
            idle = clips[0];

        var profile = ScriptableObject.CreateInstance<CharacterActionProfile>();
        var actions = new System.Collections.Generic.List<CharacterActionDefinition>
        {
            new CharacterActionDefinition("Idle", idle, true)
        };
        if (move != null)
            actions.Add(new CharacterActionDefinition("Move", move, true));
        if (attack != null)
            actions.Add(new CharacterActionDefinition("Attack", attack, false, 1f, 0.08f, 0.38f));
        if (hit != null)
            actions.Add(new CharacterActionDefinition("Hit", hit, false));
        if (death != null)
            actions.Add(new CharacterActionDefinition("Death", death, false));
        profile.Configure("Idle", actions);
        return profile;
    }

    /// <summary>按名称关键字在片段列表里找第一条匹配。</summary>
    static AnimationClip FindClip(AnimationClip[] clips, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip != null &&
                    clip.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }
}
