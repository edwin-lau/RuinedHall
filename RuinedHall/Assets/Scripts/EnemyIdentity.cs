using UnityEngine;

/// <summary>
/// 按物体名字给敌人套奖励、AI 习惯和训练木桩等身份。
/// </summary>
public static class EnemyIdentity
{
    /// <summary>Awake 时调用：名字匹配则改战斗数值/行为。</summary>
    public static void Apply(CharacterCombatAgent agent)
    {
        if (agent == null)
            return;

        string name = agent.gameObject.name;

        // 训练木桩：只挨打，不反击。
        if (Matches(name, "thor"))
        {
            agent.ApplyTrainingDummy("肉桩");
            return;
        }

        // 营地公鸡：少量金币，啄地待机。
        if (Matches(name, "rooster", "chicken"))
        {
            agent.ApplyRewards(false, "", 2);
            agent.ApplyIdleHabits(
                new[] { "Peck1", "Peck2", "Peck3", "IdleVariation" },
                new Vector2(1.4f, 3.2f),
                7.5f);
            return;
        }

        // 精英石头人：高血高伤。
        if (Matches(name, "golem"))
        {
            agent.ApplyRewards(true, "精英 · 石头人", 15);
            agent.ApplyCombatTuning(300, 34, 3.8f, 0.55f, 2.6f, 1.45f);
            return;
        }

        // 精英狐猴：中血，跑得快。
        if (Matches(name, "lemur"))
        {
            agent.ApplyRewards(true, "精英 · 狐猴", 8);
            agent.ApplyCombatTuning(96, 16, 2.05f, 0.48f, 4.3f, 1.35f);
            return;
        }

        // 精英 Triton：远程型，射程更远。
        if (Matches(name, "triton"))
        {
            agent.ApplyRewards(true, "精英 · Triton", 12);
            agent.ApplyCombatTuning(170, 18, 2.6f, 0.28f, 9.6f, 1.3f);
            return;
        }

        // Skeleton Rogue：破土现身 + 射箭。
        if (Matches(name, "skeleton") && Matches(name, "rogue"))
        {
            agent.ApplyRewards(true, "精英 · Skeleton Rogue", 10);
            agent.ApplyCombatTuning(140, 14, 14f, 1.1f, 6.2f, 1.2f);
            agent.ApplyAttackActions("Throw");
            agent.ApplySpawnFromGround(16f);
            agent.ApplyRangedThrow(
                "characters/Skeleton/assets/fbx/Skeleton_Arrow",
                18f,
                18f);
            return;
        }

        // 普通 Rogue 精英。
        if (Matches(name, "rogue"))
        {
            agent.ApplyRewards(true, "精英 · Rogue", 10);
            agent.ApplyCombatTuning(130, 15, 2.1f, 0.5f, 3.9f, 1.35f);
            return;
        }

        // 母鹿：被打前不主动攻击。
        if (Matches(name, "female", "doe"))
        {
            agent.ApplyRewards(false, "", 4);
            agent.ApplyCombatTuning(80, 10, 1.7f, 0.85f, 2.9f, 1f);
            agent.ApplyPassiveUntilHit(true);
            agent.ApplyIdleHabits(
                new[] { "Eat", "Look", "Look2", "Idle2", "Jump" },
                new Vector2(2.2f, 4.6f),
                9f);
            return;
        }

        // 公鹿：攻击时冲锋。
        if (Matches(name, "deer", "stag"))
        {
            agent.ApplyRewards(false, "", 6);
            agent.ApplyCombatTuning(90, 18, 2.6f, 0.5f, 8.8f, 1.15f);
            agent.ApplyChargeOnAttack(true);
            agent.ApplyIdleHabits(
                new[] { "Eat", "Look", "Look2", "Idle2" },
                new Vector2(2.4f, 5f),
                8f);
            return;
        }

        // 没匹配到具体种类时，至少给一份默认金币。
        if (agent.GoldReward <= 0)
            agent.ApplyRewards(agent.IsElite, agent.EliteTitle, 3);
    }

    /// <summary>名字里是否包含任一关键字（忽略大小写）。</summary>
    static bool Matches(string name, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
