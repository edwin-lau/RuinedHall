using UnityEngine;

public static class EliteActorSetup
{
    public static void EnsureNamedElites()
    {
        SkeletonRogueSetup.Ensure();
        TryEnsure("Rogue");
    }

    static void TryEnsure(string objectName)
    {
        GameObject actor = GameObject.Find(objectName);
        if (actor == null || actor.GetComponent<CharacterCombatAgent>() != null)
            return;

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

    static CharacterActionProfile BuildProfileFromAnimator(GameObject actor)
    {
        var animator = actor.GetComponent<Animator>();
        AnimationClip[] clips = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips
            : null;
        if (clips == null || clips.Length == 0)
            return null;

        AnimationClip idle = FindClip(clips, "idle", "stand");
        AnimationClip move = FindClip(clips, "walk", "run", "move");
        AnimationClip attack = FindClip(clips, "attack", "slash", "melee");
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
        if (death != null)
            actions.Add(new CharacterActionDefinition("Death", death, false));
        profile.Configure("Idle", actions);
        return profile;
    }

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
