using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 金币、精英击杀、体力的本地存档，跨场景常驻。
/// </summary>
public sealed class PlayerProgress : MonoBehaviour
{
    const string GoldKey = "RuinedHall.Gold";
    const string EliteKey = "RuinedHall.EliteKills";
    const string StaminaKey = "RuinedHall.Stamina";
    const int DefaultStamina = 10;

    static PlayerProgress _instance;

    int _gold;
    int _eliteKills;
    int _stamina;

    /// <summary>没有实例时自动创建。</summary>
    public static PlayerProgress Instance
    {
        get
        {
            if (_instance == null)
                Create();
            return _instance;
        }
    }

    public int Gold => _gold;
    public int EliteKills => _eliteKills;
    public int Stamina => _stamina;
    public static event Action Changed;

    /// <summary>进任何场景前先把进度对象拉起来。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Create();
    }

    /// <summary>DontDestroyOnLoad 单例并读盘。</summary>
    static void Create()
    {
        if (_instance != null)
            return;

        var go = new GameObject("PlayerProgress");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PlayerProgress>();
        _instance.Load();
    }

    /// <summary>敌人死亡时记金币/精英数。</summary>
    public static void NotifyKill(CharacterCombatAgent agent)
    {
        if (agent == null)
            return;
        Instance.RegisterKill(agent.IsElite, agent.GoldReward);
    }

    /// <summary>加金币；精英再加本关击杀计数并落盘。</summary>
    public void RegisterKill(bool elite, int gold)
    {
        if (gold > 0)
            _gold += gold;
        if (elite)
        {
            _eliteKills++;
            PlayerPrefs.SetInt(LevelEliteKey(), PlayerPrefs.GetInt(LevelEliteKey(), 0) + 1);
        }

        Save();
        Changed?.Invoke();
    }

    /// <summary>HUD「+体力」：随时加体力。</summary>
    public void AddStamina(int amount)
    {
        _stamina += Mathf.Max(1, amount);
        Save();
        Changed?.Invoke();
    }

    /// <summary>复活等消耗；不够则失败且不改数值。</summary>
    public bool TryConsumeStamina(int amount)
    {
        amount = Mathf.Max(1, amount);
        if (_stamina < amount)
            return false;

        _stamina -= amount;
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>从 PlayerPrefs 读金币、精英、体力。</summary>
    void Load()
    {
        _gold = PlayerPrefs.GetInt(GoldKey, 0);
        _eliteKills = PlayerPrefs.GetInt(EliteKey, 0);
        _stamina = PlayerPrefs.HasKey(StaminaKey)
            ? PlayerPrefs.GetInt(StaminaKey, DefaultStamina)
            : DefaultStamina;
    }

    /// <summary>立刻写入 PlayerPrefs。</summary>
    void Save()
    {
        PlayerPrefs.SetInt(GoldKey, _gold);
        PlayerPrefs.SetInt(EliteKey, _eliteKills);
        PlayerPrefs.SetInt(StaminaKey, _stamina);
        PlayerPrefs.Save();
    }

    /// <summary>当前场景自己的精英击杀 key，避免关卡互相覆盖。</summary>
    static string LevelEliteKey()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(scene))
            scene = "HelpOthers";
        return EliteKey + "." + scene;
    }
}
