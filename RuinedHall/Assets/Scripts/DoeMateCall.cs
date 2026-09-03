using UnityEngine;

public sealed class DoeMateCall : MonoBehaviour
{
    CharacterCombatAgent _doe;
    bool _called;

    void Awake()
    {
        Bind();
    }

    void Start()
    {
        Bind();
    }

    void Bind()
    {
        if (_doe != null)
            return;
        _doe = GetComponent<CharacterCombatAgent>();
        if (_doe != null)
            _doe.HealthChanged += OnHealth;
    }

    void OnDestroy()
    {
        if (_doe != null)
            _doe.HealthChanged -= OnHealth;
    }

    void OnHealth(int current, int max)
    {
        if (_called || max <= 0 || current * 2 > max)
            return;

        _called = true;
        DeerHerdSetup.ReleaseStag(_doe);
    }
}
