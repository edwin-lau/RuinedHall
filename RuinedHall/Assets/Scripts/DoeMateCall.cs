using UnityEngine;

/// <summary>
/// 母鹿掉到半血时放雄鹿加入战斗，只触发一次。
/// </summary>
public sealed class DoeMateCall : MonoBehaviour
{
    CharacterCombatAgent _doe;
    bool _called;

    /// <summary>尽早订阅血量。</summary>
    void Awake()
    {
        Bind();
    }

    /// <summary>Start 再绑一次，防止组件添加顺序导致没订上。</summary>
    void Start()
    {
        Bind();
    }

    /// <summary>找到母鹿战斗组件并听 HealthChanged。</summary>
    void Bind()
    {
        if (_doe != null)
            return;
        _doe = GetComponent<CharacterCombatAgent>();
        if (_doe != null)
            _doe.HealthChanged += OnHealth;
    }

    /// <summary>退订，避免销毁后还回调。</summary>
    void OnDestroy()
    {
        if (_doe != null)
            _doe.HealthChanged -= OnHealth;
    }

    /// <summary>当前血量 ≤ 一半时放雄鹿。</summary>
    void OnHealth(int current, int max)
    {
        if (_called || max <= 0 || current * 2 > max)
            return;

        _called = true;
        DeerHerdSetup.ReleaseStag(_doe);
    }
}
