using UnityEngine;

public static class AnimPlayback
{
    public static float Length(AnimationClip clip, float speed)
    {
        if (clip == null || clip.length <= 0f)
            return 0f;
        float scale = Mathf.Abs(speed);
        if (scale < 0.0001f)
            scale = 1f;
        return clip.length / scale;
    }

    public static float Length(CharacterActionDefinition action, float extraSpeed = 1f)
    {
        if (action == null)
            return 0f;
        return Length(action.Clip, action.Speed * extraSpeed);
    }

    public static float Length(Animator animator)
    {
        if (animator == null)
            return 0f;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0 && clips[0].clip != null)
            return Length(clips[0].clip, info.speed * info.speedMultiplier);

        return info.length > 0f ? info.length : 0f;
    }

    public static float NormalizedTime(Animator animator)
    {
        return animator == null ? 0f : animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
    }

    public static float ClipNormalized(float clipTime, AnimationClip clip)
    {
        if (clip == null || clip.length <= 0f)
            return 1f;
        return Mathf.Clamp01(clipTime / clip.length);
    }
}
