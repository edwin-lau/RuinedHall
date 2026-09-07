using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Configures hero2 (Character_Elf + Mixamo anims) as the playable hero:
/// Humanoid retarget, Hero2.controller, Hero2 prefab, scene wiring.
/// </summary>
public static class SetupHero2
{
    const string FlagPath = "Temp/SetupHero2.flag";
    const string ReimportAnimsFlagPath = "Temp/ReimportHero2Anims.flag";
    const string ElfMesh = "Assets/Resources/characters/hero2/Character_Elf/Mesh/Elf_Mesh.FBX";
    const string ElfPrefab = "Assets/Resources/characters/hero2/Character_Elf/Prefab/Character_Elf.prefab";
    const string AnimDir = "Assets/Resources/characters/hero2/animations";
    const string ControllerPath = "Assets/Resources/characters/hero2/Hero2.controller";
    const string Hero2PrefabPath = "Assets/Resources/characters/hero2/Hero2.prefab";
    const string ScenePath = "Assets/Scenes/HelpOthers.unity";

    static readonly (string fbx, string state, bool loop)[] Clips =
    {
        ("idle.fbx", "Idle", true),
        ("Walking.fbx", "Walk", true),
        ("Running.fbx", "Run", true),
        ("punch.fbx", "Punch", false),
        ("Jump.fbx", "Jump", false),
        ("HitToBody.fbx", "Hit", false),
        ("Punch1.fbx", "Punch1", false),
        ("punchCombo/combo1/Punch2.fbx", "Punch2", false),
        ("punchCombo/combo1/Punch3.fbx", "Punch3", false),
        ("punchCombo/combo1/Punch4.fbx", "Punch4", false),
        ("death.fbx", "Death", false),
        ("Jump/WalkJump.fbx", "WalkJump", false),
        ("Jump/RunJump.fbx", "RunJump", false),
    };

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (File.Exists(ReimportAnimsFlagPath))
        {
            File.Delete(ReimportAnimsFlagPath);
            try
            {
                ConfigureAnimationImports();
                AssetDatabase.SaveAssets();
                Debug.Log("SetupHero2: reimported Mixamo clips as Humanoid CreateFromThisModel.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("SetupHero2 anim reimport failed: " + ex);
            }
        }

        if (!File.Exists(FlagPath))
            return;
        File.Delete(FlagPath);
        try
        {
            Apply();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("SetupHero2 failed: " + ex);
        }
    }

    [MenuItem("Build/Configure Hero2 Animation Imports")]
    public static void ConfigureImportsOnly()
    {
        ConfigureAnimationImports();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            EnsureCombatStates(controller);
            foreach (var (fbx, state, _) in Clips)
            {
                AnimationClip clip = TryLoadNamedClip(fbx, state);
                if (clip == null)
                    continue;
                foreach (AnimatorStateMachine sm in controller.layers.Select(l => l.stateMachine))
                {
                    foreach (ChildAnimatorState child in sm.states)
                    {
                        if (child.state != null && child.state.name == state)
                            child.state.motion = clip;
                    }
                }
            }

            EditorUtility.SetDirty(controller);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Hero2 动画已按 Humanoid 导入，并接到 Hero2.controller。");
    }

    [MenuItem("Build/Setup Hero2 As Player")]
    public static void Apply()
    {
        ConfigureAnimationImports();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AnimatorController controller = BuildController();
        GameObject prefab = BuildHero2Prefab(controller);
        PlaceInScene(prefab);
        WireGameplayBootstrap(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SetupHero2 OK: controller + prefab ready, scene uses hero2 as player.");
    }

    static void ConfigureAnimationImports()
    {
        Avatar sourceAvatar = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ElfMesh))
        {
            if (asset is Avatar avatar && avatar.isHuman)
            {
                sourceAvatar = avatar;
                break;
            }
        }

        if (sourceAvatar == null)
        {
            var elfImporter = AssetImporter.GetAtPath(ElfMesh) as ModelImporter;
            if (elfImporter != null)
            {
                elfImporter.animationType = ModelImporterAnimationType.Human;
                elfImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                elfImporter.SaveAndReimport();
            }

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ElfMesh))
            {
                if (asset is Avatar avatar && avatar.isHuman)
                {
                    sourceAvatar = avatar;
                    break;
                }
            }
        }

        if (sourceAvatar == null)
            throw new System.Exception("Could not load Humanoid Avatar from Elf_Mesh.FBX");

        foreach (var (fbx, state, loop) in Clips)
        {
            string path = AnimDir + "/" + fbx;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("SetupHero2: missing animation " + path);
                continue;
            }

            // Must NOT CopyFromOther(Elf) — Mixamo ≠ Elf bone mapping → Rig Error, silent T-pose.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;

            // Force via SerializedObject — plain field assign sometimes leaves stale CopyFromOther in .meta.
            var so = new SerializedObject(importer);
            so.FindProperty("m_HumanoidOversampling").intValue = 1;
            SerializedProperty avatarSetupProp = so.FindProperty("m_AvatarSetup");
            if (avatarSetupProp != null)
                avatarSetupProp.intValue = (int)ModelImporterAvatarSetup.CreateFromThisModel;
            SerializedProperty sourceAvatarProp = so.FindProperty("m_LastHumanDescriptionAvatarSource");
            if (sourceAvatarProp != null)
                sourceAvatarProp.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0)
                defaults = importer.clipAnimations;

            if (defaults != null && defaults.Length > 0)
            {
                var configured = new ModelImporterClipAnimation[defaults.Length];
                for (int i = 0; i < defaults.Length; i++)
                {
                    configured[i] = defaults[i];
                    configured[i].name = i == 0 ? state : state + "_" + i;
                    configured[i].loopTime = loop;
                    configured[i].loopPose = loop;
                    bool travelJump = state == "WalkJump" || state == "RunJump";
                    configured[i].lockRootRotation = true;
                    configured[i].lockRootHeightY = true;
                    configured[i].lockRootPositionXZ = !travelJump;
                    configured[i].keepOriginalOrientation = true;
                    configured[i].keepOriginalPositionY = false;
                    configured[i].heightFromFeet = true;
                    configured[i].keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = configured;
            }

            if (state == "WalkJump" || state == "RunJump")
            {
                var clipSo = new SerializedObject(importer);
                SerializedProperty clipAnims = clipSo.FindProperty("m_ClipAnimations");
                if (clipAnims != null)
                {
                    for (int i = 0; i < clipAnims.arraySize; i++)
                    {
                        SerializedProperty clip = clipAnims.GetArrayElementAtIndex(i);
                        clip.FindPropertyRelative("loopBlendPositionXZ").boolValue = false;
                    }

                    clipSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    static AnimationClip TryLoadNamedClip(string fbx, string state)
    {
        string path = AnimDir + "/" + fbx;
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c != null &&
                                 !c.name.StartsWith("__preview__", System.StringComparison.Ordinal) &&
                                 c.name == state);
        if (clip == null)
        {
            clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => c != null &&
                                     !c.name.StartsWith("__preview__", System.StringComparison.Ordinal));
        }

        return clip;
    }

    static AnimationClip LoadNamedClip(string fbx, string state)
    {
        AnimationClip clip = TryLoadNamedClip(fbx, state);
        if (clip == null)
            throw new System.Exception("No clip in " + AnimDir + "/" + fbx);
        return clip;
    }

    static void EnsureCombatStates(AnimatorController controller)
    {
        if (controller == null || controller.layers.Length == 0)
            return;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        EnsureState(sm, "Hit", TryLoadNamedClip("HitToBody.fbx", "Hit"), new Vector3(520, 360, 0));
        EnsureState(sm, "Punch1", TryLoadNamedClip("Punch1.fbx", "Punch1"), new Vector3(520, 160, 0));
        EnsureState(sm, "Punch2", TryLoadNamedClip("punchCombo/combo1/Punch2.fbx", "Punch2"), new Vector3(740, 160, 0));
        EnsureState(sm, "Punch3", TryLoadNamedClip("punchCombo/combo1/Punch3.fbx", "Punch3"), new Vector3(740, 260, 0));
        EnsureState(sm, "Punch4", TryLoadNamedClip("punchCombo/combo1/Punch4.fbx", "Punch4"), new Vector3(740, 360, 0));
        EnsureState(sm, "Death", TryLoadNamedClip("death.fbx", "Death"), new Vector3(520, 260, 0));
        EnsureState(sm, "WalkJump", TryLoadNamedClip("Jump/WalkJump.fbx", "WalkJump"), new Vector3(520, 460, 0));
        EnsureState(sm, "RunJump", TryLoadNamedClip("Jump/RunJump.fbx", "RunJump"), new Vector3(740, 460, 0));
    }

    static void EnsureState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState found = null;
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state != null && child.state.name == name)
            {
                found = child.state;
                break;
            }
        }

        if (found == null)
            found = sm.AddState(name, position);
        if (clip != null)
            found.motion = clip;
    }

    static AnimatorController BuildController()
    {
        AnimationClip idle = LoadNamedClip("idle.fbx", "Idle");
        AnimationClip walk = LoadNamedClip("Walking.fbx", "Walk");
        AnimationClip run = LoadNamedClip("Running.fbx", "Run");
        AnimationClip punch = LoadNamedClip("punch.fbx", "Punch");
        AnimationClip jump = idle;
        string jumpPath = AnimDir + "/Jump.fbx";
        if (AssetDatabase.LoadAssetAtPath<Object>(jumpPath) != null)
            jump = LoadNamedClip("Jump.fbx", "Jump");

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // Clear default empty state if any
        foreach (ChildAnimatorState existing in sm.states.ToArray())
            sm.RemoveState(existing.state);

        AnimatorState idleState = sm.AddState("Idle", new Vector3(300, 60, 0));
        idleState.motion = idle;
        AnimatorState walkState = sm.AddState("Walk", new Vector3(300, 160, 0));
        walkState.motion = walk;
        AnimatorState runState = sm.AddState("Run", new Vector3(300, 260, 0));
        runState.motion = run;
        AnimatorState punchState = sm.AddState("Punch", new Vector3(300, 360, 0));
        punchState.motion = punch;
        AnimatorState jumpState = sm.AddState("Jump", new Vector3(520, 60, 0));
        jumpState.motion = jump;
        AnimatorState deathState = sm.AddState("Death", new Vector3(520, 260, 0));
        deathState.motion = TryLoadNamedClip("death.fbx", "Death") ?? idle;
        EnsureCombatStates(controller);

        sm.defaultState = idleState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static GameObject BuildHero2Prefab(RuntimeAnimatorController controller)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ElfPrefab);
        if (source == null)
            throw new System.Exception("Missing " + ElfPrefab);

        Avatar elfAvatar = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ElfMesh))
        {
            if (asset is Avatar avatar && avatar.isHuman)
            {
                elfAvatar = avatar;
                break;
            }
        }

        GameObject instance = Object.Instantiate(source);
        instance.name = "hero2";
        instance.transform.localScale = Vector3.one;

        var animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (elfAvatar != null)
            animator.avatar = elfAvatar;

        var cc = instance.GetComponent<CharacterController>();
        if (cc == null)
            cc = instance.AddComponent<CharacterController>();
        CharacterBodyFit.Apply(cc, instance.transform);

        if (instance.GetComponent<HeroController>() == null)
            instance.AddComponent<HeroController>();

        try
        {
            instance.tag = "Player";
        }
        catch (UnityException)
        {
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(Hero2PrefabPath) != null)
            AssetDatabase.DeleteAsset(Hero2PrefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, Hero2PrefabPath);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    static void PlaceInScene(GameObject heroPrefab)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("SetupHero2: skip scene placement while entering/playing.");
            return;
        }

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        GameObject oldHero = GameObject.Find("hero");

        Vector3 spawn = new Vector3(237.3f, 14.5f, 30f);
        Quaternion rot = Quaternion.identity;
        if (oldHero != null)
        {
            spawn = oldHero.transform.position;
            rot = oldHero.transform.rotation;
            oldHero.SetActive(false);
            oldHero.name = "hero_old";
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;
            bool isHero2 =
                string.Equals(root.name, "hero2", System.StringComparison.OrdinalIgnoreCase) ||
                PrefabUtility.GetCorrespondingObjectFromSource(root) == heroPrefab;
            if (isHero2)
                Object.DestroyImmediate(root);
        }

        GameObject hero2 = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab, scene);
        hero2.name = "hero2";
        hero2.transform.SetPositionAndRotation(spawn, rot);
        hero2.SetActive(true);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void WireGameplayBootstrap(RuntimeAnimatorController controller)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene");
        // Already placed in HelpOthers; update bootstrap serialized fields via scene objects.
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var bootstrap = root.GetComponent<GameplayBootstrap>();
            if (bootstrap == null)
                bootstrap = root.GetComponentInChildren<GameplayBootstrap>(true);
            if (bootstrap == null)
                continue;

            SerializedObject so = new SerializedObject(bootstrap);
            so.FindProperty("heroAnimator").objectReferenceValue = controller;
            so.FindProperty("heroObjectName").stringValue = "hero2";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            return;
        }
    }
}
