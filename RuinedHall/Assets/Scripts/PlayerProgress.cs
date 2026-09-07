using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Create();
    }

    static void Create()
    {
        if (_instance != null)
            return;

        var go = new GameObject("PlayerProgress");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PlayerProgress>();
        _instance.Load();
    }

    public static void NotifyKill(CharacterCombatAgent agent)
    {
        if (agent == null)
            return;
        Instance.RegisterKill(agent.IsElite, agent.GoldReward);
    }

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

    public void AddStamina(int amount)
    {
        _stamina += Mathf.Max(1, amount);
        Save();
        Changed?.Invoke();
    }

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

    void Load()
    {
        _gold = PlayerPrefs.GetInt(GoldKey, 0);
        _eliteKills = PlayerPrefs.GetInt(EliteKey, 0);
        _stamina = PlayerPrefs.HasKey(StaminaKey)
            ? PlayerPrefs.GetInt(StaminaKey, DefaultStamina)
            : DefaultStamina;
    }

    void Save()
    {
        PlayerPrefs.SetInt(GoldKey, _gold);
        PlayerPrefs.SetInt(EliteKey, _eliteKills);
        PlayerPrefs.SetInt(StaminaKey, _stamina);
        PlayerPrefs.Save();
    }

    static string LevelEliteKey()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(scene))
            scene = "HelpOthers";
        return EliteKey + "." + scene;
    }
}
