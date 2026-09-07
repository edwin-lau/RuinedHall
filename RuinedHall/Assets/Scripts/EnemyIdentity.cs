using UnityEngine;

public static class EnemyIdentity
{
    public static void Apply(CharacterCombatAgent agent)
    {
        if (agent == null)
            return;

        string name = agent.gameObject.name;
        if (Matches(name, "thor"))
        {
            agent.ApplyTrainingDummy("肉桩");
            return;
        }

        if (Matches(name, "rooster", "chicken"))
        {
            agent.ApplyRewards(false, "", 2);
            agent.ApplyIdleHabits(
                new[] { "Peck1", "Peck2", "Peck3", "IdleVariation" },
                new Vector2(1.4f, 3.2f),
                7.5f);
            return;
        }

        if (Matches(name, "golem"))
        {
            agent.ApplyRewards(true, "精英 · 石头人", 15);
            agent.ApplyCombatTuning(300, 34, 3.8f, 0.55f, 2.6f, 1.45f);
            return;
        }

        if (Matches(name, "lemur"))
        {
            agent.ApplyRewards(true, "精英 · 狐猴", 8);
            agent.ApplyCombatTuning(96, 16, 2.05f, 0.48f, 4.3f, 1.35f);
            return;
        }

        if (Matches(name, "triton"))
        {
            agent.ApplyRewards(true, "精英 · Triton", 12);
            agent.ApplyCombatTuning(170, 18, 2.6f, 0.28f, 9.6f, 1.3f);
            return;
        }

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

        if (Matches(name, "rogue"))
        {
            agent.ApplyRewards(true, "精英 · Rogue", 10);
            agent.ApplyCombatTuning(130, 15, 2.1f, 0.5f, 3.9f, 1.35f);
            return;
        }

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

        if (agent.GoldReward <= 0)
            agent.ApplyRewards(agent.IsElite, agent.EliteTitle, 3);
    }

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
