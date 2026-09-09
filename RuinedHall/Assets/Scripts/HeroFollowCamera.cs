using UnityEngine;

/// <summary>
/// 跟随玩家的锁定视角相机，并叠一层打击镜头 kick。
/// </summary>
public class HeroFollowCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float followSmoothing = 14f;

    Quaternion _lockedRotation;
    Vector3 _offset;
    bool _hasView;
    bool _snapNow;

    Vector3 _kick;
    const float KickDamp = 18f;
    const float KickMax = 0.16f;

    /// <summary>绑定跟随目标和设计好的机位偏移/朝向。</summary>
    public void Bind(Transform followTarget, Vector3 worldOffset, Quaternion worldRotation)
    {
        target = followTarget;
        _offset = worldOffset;
        _lockedRotation = worldRotation;
        _hasView = target != null;
        _snapNow = true;
        Apply(1f);
    }

    /// <summary>每帧跟上角色；刹车时立刻贴住，避免画面里人往后滑。</summary>
    void LateUpdate()
    {
        if (target == null || !_hasView)
            return;

        float speed = 0f;
        var hero = target.GetComponent<HeroController>();
        if (hero != null)
            speed = hero.PlanarSpeed;

        // 停下时 t=1 瞬移跟上；移动中按平滑系数插值
        float t = _snapNow || followSmoothing <= 0f || speed <= 0.35f
            ? 1f
            : 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        _snapNow = false;
        // kick 用不缩放时间衰减，顿帧期间镜头后坐仍能看见
        _kick = Vector3.Lerp(_kick, Vector3.zero, 1f - Mathf.Exp(-KickDamp * Time.unscaledDeltaTime));
        Apply(t);
    }

    /// <summary>命中时给相机一个水平后坐，有上限防止连招晃晕。</summary>
    public void Kick(Vector3 worldDir, float meters)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f)
            worldDir = -transform.forward;
        _kick += worldDir.normalized * Mathf.Max(0f, meters);
        if (_kick.magnitude > KickMax)
            _kick = _kick.normalized * KickMax;
    }

    /// <summary>把相机放到 目标 + 设计偏移 + kick。</summary>
    void Apply(float t)
    {
        Vector3 desired = target.position + _offset + _kick;
        transform.position = t >= 1f
            ? desired
            : Vector3.Lerp(transform.position, desired, t);
        transform.rotation = _lockedRotation;
    }
}
