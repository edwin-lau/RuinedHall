using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CharacterFrameworkSetup
{
    const string ScenePath = "Assets/Scenes/HelpOthers.unity";
    const string RoosterFolder = "Assets/Resources/characters/others/Rooster";
    const string GolemPath =
        "Assets/Resources/characters/others/Golem/golem_attack.fbx";
    const string ProfilesFolder = "Assets/Resources/characters/Profiles";
    const string RoosterProfilePath = ProfilesFolder + "/RoosterActions.asset";
    const string GolemProfilePath = ProfilesFolder + "/GolemActions.asset";
    const string SessionKey = "RuinedHall.CharacterFrameworkSetup.v1";

    static CharacterFrameworkSetup()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += ConfigureWhenReady;
    }

    [MenuItem("Tools/Characters/Framework/Configure Rooster and Golem")]
    public static void ConfigureRoosterAndGolem()
    {
        EnsureProfilesFolder();
        CharacterActionProfile roosterProfile = CreateRoosterProfile();
        CharacterActionProfile golemProfile = CreateGolemProfile();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject rooster = GameObject.Find("rooster");
        if (rooster == null)
            throw new InvalidOperationException("Scene object 'rooster' was not found.");

        GameObject golem = GameObject.Find("golem_attack");
        if (golem == null)
            throw new InvalidOperationException("Scene object 'golem_attack' was not found.");

        ConfigureActor(
            rooster,
            roosterProfile,
            new ActorConfig
            {
                IdleVariations = new[] { "Peck1", "Peck2", "Peck3", "IdleVariation" },
                AttackActions = new[] { "Attack1", "Attack2", "Attack3" },
                DetectionRange = 14f,
                LoseInterestRange = 20f,
                AttackRange = 1.35f,
                MoveSpeed = 2.4f,
                RotationSpeed = 420f,
                MaxHealth = 40,
                AttackDamage = 15,
                AttackCooldown = 1.15f,
                CorpseHoldDuration = 1.2f,
                FadeDuration = 2.5f
            });

        ConfigureActor(
            golem,
            golemProfile,
            new ActorConfig
            {
                IdleVariations = Array.Empty<string>(),
                AttackActions = new[] { "Attack1", "Attack2" },
                DetectionRange = 12f,
                LoseInterestRange = 20f,
                AttackRange = 3.8f,
                MoveSpeed = 2.6f,
                RotationSpeed = 280f,
                MaxHealth = 300,
                AttackDamage = 34,
                AttackCooldown = 0.55f,
                CorpseHoldDuration = 1.5f,
                FadeDuration = 3f
            });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharacterFrameworkSetup] COMPLETE rooster=1 golem=1");
    }

    [MenuItem("Tools/Characters/Framework/Configure Wildlife")]
    public static void ConfigureWildlife()
    {
        EnsureProfilesFolder();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        CharacterActionProfile lemurProfile = CreateMappedProfile(
            "Assets/Resources/characters/others/lemur/lemur.fbx",
            ProfilesFolder + "/LemurActions.asset",
            "Idle",
            ("Idle", new[] { "idle" }, true),
            ("Move", new[] { "run" }, true),
            ("Attack", new[] { "attack" }, false));

        CharacterActionProfile tritonProfile = CreateTritonProfile();
        CharacterActionProfile rabbitProfile = CreateMappedProfile(
            "Assets/Resources/characters/others/rabbit/rabbit.fbx",
            ProfilesFolder + "/RabbitActions.asset",
            "Idle",
            ("Idle", new[] { "sitting.000", "sitting", "basic" }, true),
            ("Move", new[] { "running", "run" }, true),
            ("Jump", new[] { "jump" }, false),
            ("Death", new[] { "dying.000", "dead" }, false));

        CharacterActionProfile eagleProfile = CreateMappedProfile(
            "Assets/Resources/characters/others/eagle/Eagle.fbx",
            ProfilesFolder + "/EagleActions.asset",
            "Idle",
            ("Idle", new[] { "eaglearmature|idle", "eagle|idle", "idle" }, true),
            ("Fly", new[] { "flying", "fly" }, true));

        GameObject lemur = GameObject.Find("lemur");
        GameObject triton = GameObject.Find("triton");
        GameObject rabbit = GameObject.Find("rabbit");
        GameObject eagle = GameObject.Find("Eagle");
        GameObject skeletonRogue = GameObject.Find("Skeleton_Rogue (1)") ??
                                   GameObject.Find("Skeleton_Rogue");
        GameObject rogue = GameObject.Find("Rogue");
        if (lemur == null || triton == null || rabbit == null || eagle == null)
            throw new InvalidOperationException(
                "Scene is missing lemur, triton, rabbit, or Eagle.");

        ConfigureActor(
            lemur,
            lemurProfile,
            new ActorConfig
            {
                AttackActions = new[] { "Attack" },
                DetectionRange = 11f,
                LoseInterestRange = 18f,
                AttackRange = 2.05f,
                MoveSpeed = 4.3f,
                RotationSpeed = 520f,
                MaxHealth = 48,
                AttackDamage = 16,
                AttackCooldown = 0.48f,
                CorpseHoldDuration = 1f,
                FadeDuration = 2f
            });

        ConfigureActor(
            triton,
            tritonProfile,
            new ActorConfig
            {
                AttackActions = new[] { "Attack1", "Attack2" },
                DetectionRange = 16f,
                LoseInterestRange = 28f,
                AttackRange = 2.6f,
                MoveSpeed = 9.6f,
                RotationSpeed = 560f,
                MaxHealth = 85,
                AttackDamage = 18,
                AttackCooldown = 0.28f,
                CorpseHoldDuration = 1.3f,
                FadeDuration = 2.4f,
                SleepUntilHit = true,
                SleepAction = "Sleep",
                WakeAction = "Wake",
                FallAsleepAction = "FallAsleep",
                RetreatAfterPlayerHits = 2
            });

        ConfigureActor(
            rabbit,
            rabbitProfile,
            new ActorConfig { Role = ActorRole.Prey });

        ConfigureActor(
            eagle,
            eagleProfile,
            new ActorConfig { Role = ActorRole.Flyer });

        if (skeletonRogue != null)
        {
            CharacterActionProfile skeletonProfile = CreateMappedProfileFromSources(
                new[]
                {
                    "Assets/Resources/characters/Skeleton/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx",
                    "Assets/Resources/characters/Skeleton/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx"
                },
                ProfilesFolder + "/SkeletonRogueActions.asset",
                "Idle",
                ("Idle", new[] { "idle_a", "idle" }, true),
                ("Idle2", new[] { "idle_b" }, true),
                ("Move", new[] { "running_a", "running" }, true),
                ("Hit", new[] { "hit_a", "hit" }, false),
                ("Death", new[] { "death_a", "death" }, false),
                ("Spawn", new[] { "spawn_ground" }, false),
                ("Throw", new[] { "throw" }, false),
                ("Attack", new[] { "throw" }, false));
            ConfigureActor(
                skeletonRogue,
                skeletonProfile,
                new ActorConfig
                {
                    AttackActions = new[] { "Throw" },
                    IdleVariations = new[] { "Idle2" },
                    DetectionRange = 20f,
                    LoseInterestRange = 28f,
                    AttackRange = 14f,
                    MoveSpeed = 6.2f,
                    RotationSpeed = 400f,
                    MaxHealth = 140,
                    AttackDamage = 14,
                    AttackCooldown = 1.1f,
                    CorpseHoldDuration = 1f,
                    FadeDuration = 2f
                });
        }

        if (rogue != null)
        {
            CharacterActionProfile rogueProfile = CreateMappedProfile(
                "Assets/Resources/characters/Adventur/Characters/fbx/Rogue.fbx",
                ProfilesFolder + "/RogueActions.asset",
                "Idle",
                ("Idle", new[] { "idle", "stand" }, true),
                ("Move", new[] { "walk", "run", "move" }, true),
                ("Attack", new[] { "attack", "slash", "melee" }, false),
                ("Death", new[] { "death", "die" }, false));
            ConfigureActor(
                rogue,
                rogueProfile,
                new ActorConfig
                {
                    AttackActions = new[] { "Attack" },
                    DetectionRange = 12f,
                    LoseInterestRange = 20f,
                    AttackRange = 2.1f,
                    MoveSpeed = 3.9f,
                    RotationSpeed = 460f,
                    MaxHealth = 65,
                    AttackDamage = 15,
                    AttackCooldown = 0.5f,
                    CorpseHoldDuration = 1f,
                    FadeDuration = 2f
                });
        }

        GameObject doe = GameObject.Find("deer-female-mesh");
        GameObject stag = GameObject.Find("deer1_textured");
        if (doe != null)
        {
            CharacterActionProfile doeProfile = CreateMappedProfile(
                "Assets/Resources/characters/others/doe/doe.fbx",
                ProfilesFolder + "/DoeActions.asset",
                "Idle",
                ("Idle", new[] { "idle" }, true),
                ("Idle2", new[] { "idle 1", "idle.001" }, false),
                ("Look", new[] { "idle 1", "look" }, false),
                ("Move", new[] { "run", "walk" }, true),
                ("Jump", new[] { "run" }, false),
                ("Attack", new[] { "run" }, false),
                ("Death", new[] { "die", "death" }, false));
            ConfigureActor(
                doe,
                doeProfile,
                new ActorConfig
                {
                    AttackActions = new[] { "Attack" },
                    IdleVariations = new[] { "Look", "Idle2", "Jump" },
                    DetectionRange = 10f,
                    LoseInterestRange = 16f,
                    AttackRange = 1.7f,
                    MoveSpeed = 2.9f,
                    RotationSpeed = 360f,
                    MaxHealth = 80,
                    AttackDamage = 10,
                    AttackCooldown = 0.85f,
                    CorpseHoldDuration = 1f,
                    FadeDuration = 2f,
                    PassiveUntilHit = true
                });
            if (doe.GetComponent<DoeMateCall>() == null)
                doe.AddComponent<DoeMateCall>();
        }

        if (stag != null)
        {
            CharacterActionProfile stagProfile = CreateMappedProfile(
                "Assets/Resources/characters/deer/deer1_textured.fbx",
                ProfilesFolder + "/StagActions.asset",
                "Idle",
                ("Idle", new[] { "armature|idle" }, true),
                ("Idle2", new[] { "idle.001" }, false),
                ("Eat", new[] { "eat" }, false),
                ("Look", new[] { "lookaround.000" }, false),
                ("Look2", new[] { "lookaround.001" }, false),
                ("Move", new[] { "run" }, true),
                ("Jump", new[] { "run" }, false),
                ("Attack", new[] { "run" }, false),
                ("Death", new[] { "die.000", "die" }, false));
            ConfigureActor(
                stag,
                stagProfile,
                new ActorConfig
                {
                    AttackActions = new[] { "Attack" },
                    IdleVariations = new[] { "Eat", "Look", "Look2" },
                    DetectionRange = 16f,
                    LoseInterestRange = 26f,
                    AttackRange = 2.6f,
                    MoveSpeed = 8.8f,
                    RotationSpeed = 480f,
                    MaxHealth = 90,
                    AttackDamage = 18,
                    AttackCooldown = 0.5f,
                    CorpseHoldDuration = 1f,
                    FadeDuration = 2f,
                    ChargeOnAttack = true
                });
        }

        if (rabbit.GetComponent<RabbitEagleHunt>() == null)
            rabbit.AddComponent<RabbitEagleHunt>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharacterFrameworkSetup] COMPLETE wildlife lemur/triton/rabbit/eagle");
    }

    [MenuItem("Tools/Characters/Framework/Create From Selected FBX")]
    public static void CreateFromSelectedFbx()
    {
        GameObject selected = Selection.activeGameObject ??
                              Selection.activeObject as GameObject;
        if (selected == null)
            throw new InvalidOperationException("Select an FBX asset or scene FBX object.");

        GameObject source = AssetDatabase.Contains(selected)
            ? selected
            : PrefabUtility.GetCorrespondingObjectFromSource(selected);
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (!sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected object is not backed by an FBX.");

        string profilePath = Path.ChangeExtension(sourcePath, null) + "_Actions.asset";
        CharacterActionProfile profile = CreateInferredProfile(sourcePath, profilePath);

        if (!AssetDatabase.Contains(selected))
        {
            ConfigureActor(selected, profile, ActorConfig.Default);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Debug.Log($"[CharacterFrameworkSetup] Configured scene FBX '{selected.name}'.");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Could not instantiate {sourcePath}.");

        try
        {
            ConfigureActor(instance, profile, ActorConfig.Default);
            string prefabPath = Path.ChangeExtension(sourcePath, null) + "_Character.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Log($"[CharacterFrameworkSetup] Created '{prefabPath}'.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static void ConfigureWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ConfigureWhenReady;
            return;
        }

        try
        {
            ConfigureRoosterAndGolem();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError("[CharacterFrameworkSetup] FAILED");
        }
    }

    static CharacterActionProfile CreateRoosterProfile()
    {
        AnimationClip Clip(string name) =>
            RequireClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{RoosterFolder}/{name}.anim"), name);

        Color hitColor = new Color(1f, 0.72f, 0.12f, 1f);
        var hitFx = new CharacterEffectCue(
            CharacterEffectKind.HitSpark,
            0.38f,
            0.65f,
            hitColor,
            0.9f,
            localPosition: new Vector3(0f, 0.8f, 0.7f));
        var deathFx = new CharacterEffectCue(
            CharacterEffectKind.DustBurst,
            0.42f,
            1.1f,
            new Color(0.55f, 0.42f, 0.25f, 0.8f),
            1.1f,
            localPosition: new Vector3(0f, 0.1f, 0f));

        return CreateOrUpdateProfile(
            RoosterProfilePath,
            "Idle",
            new[]
            {
                new CharacterActionDefinition("Idle", Clip("Standing"), true, 1f, 0.08f),
                new CharacterActionDefinition(
                    "IdleVariation", Clip("Idle1"), false, 1f, 0.12f),
                new CharacterActionDefinition("Alert", Clip("Screaming"), false),
                new CharacterActionDefinition("Move", Clip("Walking"), true, 1f, 0.1f),
                new CharacterActionDefinition("Peck1", Clip("Pecking.001"), false, 1f, 0.1f),
                new CharacterActionDefinition("Peck2", Clip("Pecking.002"), false, 1f, 0.1f),
                new CharacterActionDefinition("Peck3", Clip("Pecking.003"), false, 1f, 0.1f),
                new CharacterActionDefinition(
                    "Attack1", Clip("Pecking.001"), false, 1f, 0.06f, 0.38f, hitFx),
                new CharacterActionDefinition(
                    "Attack2", Clip("Pecking.002"), false, 1f, 0.06f, 0.38f, hitFx),
                new CharacterActionDefinition(
                    "Attack3", Clip("Pecking.003"), false, 1f, 0.06f, 0.38f, hitFx),
                new CharacterActionDefinition(
                    "Death", Clip("Dying"), false, 1f, 0.05f, 0.4f, deathFx)
            });
    }

    static CharacterActionProfile CreateGolemProfile()
    {
        AnimationClip Clip(string name) => RequireClip(FindEmbeddedClip(GolemPath, name), name);
        Color stoneColor = new Color(0.76f, 0.66f, 0.48f, 1f);
        var heavyHitFx = new CharacterEffectCue(
            CharacterEffectKind.DustBurst,
            0.72f,
            1.2f,
            stoneColor,
            2.2f,
            localPosition: new Vector3(0f, 0.15f, 1.2f));
        var quickHitFx = new CharacterEffectCue(
            CharacterEffectKind.HitSpark,
            0.24f,
            0.75f,
            new Color(1f, 0.55f, 0.12f, 1f),
            1.8f,
            localPosition: new Vector3(0f, 1.1f, 1.1f));
        var deathFx = new CharacterEffectCue(
            CharacterEffectKind.DustBurst,
            0.45f,
            1.5f,
            stoneColor,
            2.8f);

        return CreateOrUpdateProfile(
            GolemProfilePath,
            "Idle",
            new[]
            {
                new CharacterActionDefinition(
                    "Idle", Clip("Armature|idle"), true, 1f, 0.15f),
                new CharacterActionDefinition(
                    "Move", Clip("Armature|walk"), true, 1f, 0.12f),
                new CharacterActionDefinition(
                    "Attack1", Clip("Armature|attack"), false, 1f, 0.1f, 0.72f, heavyHitFx),
                new CharacterActionDefinition(
                    "Attack2", Clip("Armature|attack2"), false, 1f, 0.08f, 0.24f, quickHitFx),
                new CharacterActionDefinition(
                    "Hit", Clip("Armature|flinch"), false, 1f, 0.06f),
                new CharacterActionDefinition(
                    "Death", Clip("Armature|die"), false, 1f, 0.08f, 0.45f, deathFx)
            });
    }

    static CharacterActionProfile CreateTritonProfile()
    {
        AnimationClip Clip(string fileName) =>
            RequireClip(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    $"Assets/Resources/characters/triton/{fileName}.anim"),
                fileName);

        return CreateOrUpdateProfile(
            ProfilesFolder + "/TritonActions.asset",
            "Sleep",
            new[]
            {
                new CharacterActionDefinition("Sleep", Clip("ArmaTriton_sleep"), true, 1f, 0.12f),
                new CharacterActionDefinition("Wake", Clip("ArmaTriton_wakeup"), false, 1f, 0.08f),
                new CharacterActionDefinition(
                    "FallAsleep", Clip("ArmaTriton_fallasleep"), false, 1f, 0.1f),
                new CharacterActionDefinition("Idle", Clip("ArmaTriton_wait"), true, 1f, 0.12f),
                new CharacterActionDefinition("Move", Clip("ArmaTriton_walk"), true, 1f, 0.08f),
                new CharacterActionDefinition(
                    "Attack1", Clip("ArmaTriton_attack"), false, 1f, 0.06f, 0.28f),
                new CharacterActionDefinition(
                    "Attack2", Clip("ArmaTriton_attack_02"), false, 1f, 0.06f, 0.22f),
                new CharacterActionDefinition("Death", Clip("ArmaTriton_die"), false, 1f, 0.08f)
            });
    }

    static CharacterActionProfile CreateMappedProfileFromSources(
        IEnumerable<string> sourcePaths,
        string profilePath,
        string defaultAction,
        params (string id, string[] nameParts, bool loop)[] map)
    {
        List<AnimationClip> clips = new();
        foreach (string sourcePath in sourcePaths)
        {
            clips.AddRange(
                AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                    .OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)));
        }

        var definitions = new List<CharacterActionDefinition>();
        foreach ((string id, string[] nameParts, bool loop) in map)
        {
            AnimationClip clip = clips.FirstOrDefault(candidate =>
                nameParts.Any(part =>
                    candidate.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0));
            if (clip != null)
                definitions.Add(new CharacterActionDefinition(id, clip, loop));
        }

        if (definitions.Count == 0)
            throw new InvalidOperationException($"No mapped clips in {profilePath}.");

        string resolvedDefault = definitions.Any(action => action.Id == defaultAction)
            ? defaultAction
            : definitions[0].Id;
        return CreateOrUpdateProfile(profilePath, resolvedDefault, definitions);
    }

    static CharacterActionProfile CreateMappedProfile(
        string sourcePath,
        string profilePath,
        string defaultAction,
        params (string id, string[] nameParts, bool loop)[] map)
    {
        return CreateMappedProfileFromSources(
            new[] { sourcePath },
            profilePath,
            defaultAction,
            map);
    }

    static CharacterActionProfile CreateInferredProfile(
        string sourcePath,
        string profilePath)
    {
        var definitions = new List<CharacterActionDefinition>();
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                     .OfType<AnimationClip>()
                     .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)))
        {
            string baseId = InferActionId(clip.name);
            counters.TryGetValue(baseId, out int count);
            counters[baseId] = ++count;
            string id = count == 1 ? baseId : baseId + count;
            bool loop = baseId is "Idle" or "Move";
            definitions.Add(new CharacterActionDefinition(id, clip, loop));
        }

        if (definitions.Count == 0)
            throw new InvalidOperationException($"No animation clips were found in {sourcePath}.");

        string defaultAction = definitions.Any(action => action.Id == "Idle")
            ? "Idle"
            : definitions[0].Id;
        return CreateOrUpdateProfile(profilePath, defaultAction, definitions);
    }

    static string InferActionId(string clipName)
    {
        string lower = clipName.ToLowerInvariant();
        if (lower.Contains("idle") || lower.Contains("stand"))
            return "Idle";
        if (lower.Contains("walk") || lower.Contains("run") || lower.Contains("move"))
            return "Move";
        if (lower.Contains("death") || lower.Contains("die") || lower.Contains("dying"))
            return "Death";
        if (lower.Contains("hit") || lower.Contains("flinch") || lower.Contains("hurt"))
            return "Hit";
        if (lower.Contains("attack") || lower.Contains("peck") || lower.Contains("punch"))
            return "Attack";
        return SanitizeId(clipName);
    }

    static string SanitizeId(string value)
    {
        char[] characters = value
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        return new string(characters).Trim('_');
    }

    static CharacterActionProfile CreateOrUpdateProfile(
        string path,
        string defaultAction,
        IEnumerable<CharacterActionDefinition> definitions)
    {
        CharacterActionProfile profile =
            AssetDatabase.LoadAssetAtPath<CharacterActionProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CharacterActionProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        profile.Configure(defaultAction, definitions);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static void ConfigureActor(
        GameObject actor,
        CharacterActionProfile profile,
        ActorConfig config)
    {
        Animator animator = actor.GetComponent<Animator>();
        if (animator == null)
            animator = actor.AddComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterActionPlayer player = actor.GetComponent<CharacterActionPlayer>();
        if (player == null)
            player = actor.AddComponent<CharacterActionPlayer>();
        var playerObject = new SerializedObject(player);
        playerObject.FindProperty("profile").objectReferenceValue = profile;
        playerObject.ApplyModifiedPropertiesWithoutUndo();

        if (config.Role == ActorRole.Flyer)
        {
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(player);
            return;
        }

        CharacterController controller = actor.GetComponent<CharacterController>();
        if (controller == null)
            controller = actor.AddComponent<CharacterController>();
        FitCharacterController(actor.transform, controller);

        if (config.Role != ActorRole.Combat)
        {
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(player);
            return;
        }

        CharacterCombatAgent agent = actor.GetComponent<CharacterCombatAgent>();
        if (agent == null)
            agent = actor.AddComponent<CharacterCombatAgent>();
        var agentObject = new SerializedObject(agent);
        SetStringArray(agentObject.FindProperty("attackActions"), config.AttackActions);
        SetStringArray(agentObject.FindProperty("idleVariations"), config.IdleVariations);
        agentObject.FindProperty("detectionRange").floatValue = config.DetectionRange;
        agentObject.FindProperty("loseInterestRange").floatValue = config.LoseInterestRange;
        agentObject.FindProperty("attackRange").floatValue = config.AttackRange;
        agentObject.FindProperty("moveSpeed").floatValue = config.MoveSpeed;
        agentObject.FindProperty("rotationSpeed").floatValue = config.RotationSpeed;
        agentObject.FindProperty("maxHealth").intValue = config.MaxHealth;
        agentObject.FindProperty("attackDamage").intValue = config.AttackDamage;
        agentObject.FindProperty("attackCooldown").floatValue = config.AttackCooldown;
        agentObject.FindProperty("corpseHoldDuration").floatValue =
            config.CorpseHoldDuration;
        agentObject.FindProperty("fadeDuration").floatValue = config.FadeDuration;
        var sleepUntilHit = agentObject.FindProperty("sleepUntilHit");
        if (sleepUntilHit != null)
            sleepUntilHit.boolValue = config.SleepUntilHit;
        var sleepAction = agentObject.FindProperty("sleepAction");
        if (sleepAction != null)
            sleepAction.stringValue = config.SleepAction ?? "Sleep";
        var wakeAction = agentObject.FindProperty("wakeAction");
        if (wakeAction != null)
            wakeAction.stringValue = config.WakeAction ?? "Wake";
        var fallAsleepAction = agentObject.FindProperty("fallAsleepAction");
        if (fallAsleepAction != null)
            fallAsleepAction.stringValue = config.FallAsleepAction ?? "FallAsleep";
        var retreatHits = agentObject.FindProperty("retreatAfterPlayerHits");
        if (retreatHits != null)
            retreatHits.intValue = config.RetreatAfterPlayerHits;
        var passiveUntilHit = agentObject.FindProperty("passiveUntilHit");
        if (passiveUntilHit != null)
            passiveUntilHit.boolValue = config.PassiveUntilHit;
        var chargeOnAttack = agentObject.FindProperty("chargeOnAttack");
        if (chargeOnAttack != null)
            chargeOnAttack.boolValue = config.ChargeOnAttack;
        agentObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(agent);
    }

    static void FitCharacterController(Transform root, CharacterController controller)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        Vector3 scale = root.lossyScale;
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        float scaleXZ = Mathf.Max(
            0.0001f,
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float localHeight = Mathf.Max(0.5f, worldBounds.size.y / scaleY);
        float localRadius = Mathf.Clamp(
            Mathf.Max(worldBounds.extents.x, worldBounds.extents.z) / scaleXZ * 0.5f,
            localHeight * 0.12f,
            localHeight * 0.25f);

        controller.center = root.InverseTransformPoint(worldBounds.center);
        controller.height = localHeight;
        controller.radius = localRadius;
        controller.slopeLimit = 50f;
        controller.stepOffset = 0f;
        controller.skinWidth = Mathf.Max(0.01f, localRadius * 0.02f);
        controller.minMoveDistance = 0f;
    }

    static void SetStringArray(SerializedProperty property, IReadOnlyList<string> values)
    {
        property.arraySize = values?.Count ?? 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    static AnimationClip FindEmbeddedClip(string assetPath, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == clipName);
    }

    static AnimationClip RequireClip(AnimationClip clip, string name)
    {
        if (clip == null)
            throw new InvalidOperationException($"Animation clip '{name}' was not found.");
        return clip;
    }

    static void EnsureProfilesFolder()
    {
        if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            AssetDatabase.CreateFolder("Assets/Resources/characters", "Profiles");
    }

    enum ActorRole
    {
        Combat,
        Prey,
        Flyer
    }

    struct ActorConfig
    {
        public ActorRole Role;
        public string[] AttackActions;
        public string[] IdleVariations;
        public float DetectionRange;
        public float LoseInterestRange;
        public float AttackRange;
        public float MoveSpeed;
        public float RotationSpeed;
        public int MaxHealth;
        public int AttackDamage;
        public float AttackCooldown;
        public float CorpseHoldDuration;
        public float FadeDuration;
        public bool SleepUntilHit;
        public string SleepAction;
        public string WakeAction;
        public string FallAsleepAction;
        public int RetreatAfterPlayerHits;
        public bool PassiveUntilHit;
        public bool ChargeOnAttack;

        public static ActorConfig Default => new()
        {
            Role = ActorRole.Combat,
            AttackActions = new[] { "Attack", "Attack2", "Attack3" },
            IdleVariations = Array.Empty<string>(),
            DetectionRange = 10f,
            LoseInterestRange = 18f,
            AttackRange = 1.5f,
            MoveSpeed = 2.5f,
            RotationSpeed = 360f,
            MaxHealth = 50,
            AttackDamage = 10,
            AttackCooldown = 1.25f,
            CorpseHoldDuration = 1f,
            FadeDuration = 2f
        };
    }
}
