using UnityEngine;

/// <summary>
/// 动画时长与归一化时间的换算，避免各处手写 clip.length / speed。
/// </summary>
public static class AnimPlayback
{
    /// <summary>按播放速度换算片段真实时长（秒）。</summary>
    public static float Length(AnimationClip clip, float speed)
    {
        if (clip == null || clip.length <= 0f)
            return 0f;
        float scale = Mathf.Abs(speed);
        if (scale < 0.0001f)
            scale = 1f;
        return clip.length / scale;
    }

    /// <summary>动作配置上的时长，再乘一层额外速度。</summary>
    public static float Length(CharacterActionDefinition action, float extraSpeed = 1f)
    {
        if (action == null)
            return 0f;
        return Length(action.Clip, action.Speed * extraSpeed);
    }

    /// <summary>当前 Animator 0 层正在播的片段时长。</summary>
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

    /// <summary>当前 0 层状态的归一化时间（可大于 1）。</summary>
    public static float NormalizedTime(Animator animator)
    {
        return animator == null ? 0f : animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
    }

    /// <summary>把片段内秒数换成 0–1，方便对齐命中帧。</summary>
    public static float ClipNormalized(float clipTime, AnimationClip clip)
    {
        if (clip == null || clip.length <= 0f)
            return 1f;
        return Mathf.Clamp01(clipTime / clip.length);
    }
}
