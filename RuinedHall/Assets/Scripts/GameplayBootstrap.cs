using System.Collections;
using UnityEngine;

public class GameplayBootstrap : MonoBehaviour
{
    [SerializeField] RuntimeAnimatorController heroAnimator;
    [SerializeField] string heroObjectName = "hero2";
    [SerializeField] string heroPrefabResource = "characters/hero2/Hero2";
    [Header("替换角色后的速度（留 0 则跟原来 hero2 一样）")]
    [SerializeField] float replacedWalkSpeed;
    [SerializeField] float replacedRunSpeed;

    void Awake()
    {
        DisableLegacyInputShowcases();
        StartCoroutine(Boot());
    }

    IEnumerator Boot()
    {
        Camera main = Camera.main;
        Vector3 designedCameraPos = main != null ? main.transform.position : Vector3.zero;
        Quaternion designedCameraRot = main != null ? main.transform.rotation : Quaternion.identity;
        float designedFov = main != null ? main.fieldOfView : 72.4f;
        float designedNear = main != null ? main.nearClipPlane : 0.3f;
        float designedFar = main != null ? main.farClipPlane : 1000f;

        GameObject placeholder = ResolveHeroObject();
        Vector3 designedHeroPos = placeholder != null ? placeholder.transform.position : Vector3.zero;
        Quaternion designedHeroRot = placeholder != null ? placeholder.transform.rotation : Quaternion.identity;
        CharacterController designedBody = placeholder != null
            ? placeholder.GetComponent<CharacterController>()
            : null;
        HeroController designedHero = placeholder != null
            ? placeholder.GetComponent<HeroController>()
            : null;
        float designedHeroHeight = placeholder != null
            ? CharacterBodyFit.WorldHeight(placeholder.transform)
            : 0f;

        GameObject heroObject = placeholder;
        bool keepDesignedPose = false;
        SidekickLook look = SidekickLook.Load();
        if (look != null)
        {
            if (placeholder != null)
                placeholder.SetActive(false);

            var task = SidekickWardrobe.CreatePlayable(look);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted || task.Result == null)
            {
                Debug.LogError("GameplayBootstrap: Sidekick 角色生成失败，继续用 hero2。\n" + task.Exception);
                if (placeholder != null)
                {
                    placeholder.SetActive(true);
                    heroObject = placeholder;
                }
            }
            else
            {
                heroObject = task.Result;
                heroObject.transform.SetPositionAndRotation(designedHeroPos, designedHeroRot);
                MatchWorldHeight(heroObject.transform, designedHeroHeight);
                keepDesignedPose = true;
            }
        }

        if (heroObject == null)
        {
            Debug.LogError("GameplayBootstrap: 找不到 hero2 / hero，也无法从 Resources 生成。");
            yield break;
        }

        GameObject legacyHero = GameObject.Find("hero");
        if (legacyHero != null && legacyHero != heroObject)
            legacyHero.SetActive(false);

        Transform mixamo = heroObject.transform.Find("MixamoHero");
        if (mixamo != null)
            Destroy(mixamo.gameObject);

        heroObject.name = "hero2";
        heroObject.SetActive(true);
        DisableOtherHeroes(heroObject);

        HeroController hero = SetupHero(heroObject, designedBody, designedHero, designedHeroHeight);
        if (!keepDesignedPose)
            SnapHeroToGround(heroObject.transform);
        SetupCamera(
            heroObject.transform,
            designedCameraPos,
            designedCameraRot,
            designedHeroPos,
            designedFov,
            designedNear,
            designedFar);
        SetupHud(hero);
        SetupRoosterSpawner(heroObject.transform);
        SetupWildlife();
        DeerHerdSetup.Ensure();
        EliteActorSetup.EnsureNamedElites();
    }

    GameObject ResolveHeroObject()
    {
        GameObject heroObject =
            FindByNameIgnoreCase(heroObjectName) ??
            FindByNameIgnoreCase("hero2") ??
            FindByNameIgnoreCase("Hero2") ??
            FindByNameIgnoreCase("hero");
        if (heroObject != null)
            return heroObject;

        GameObject prefab = Resources.Load<GameObject>(heroPrefabResource);
        if (prefab == null)
            return null;

        heroObject = Instantiate(prefab);
        heroObject.name = "hero2";
        return heroObject;
    }

    static GameObject FindByNameIgnoreCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (string.Equals(go.name, name, System.StringComparison.OrdinalIgnoreCase))
                return go;
        }

        return null;
    }

    static void DisableOtherHeroes(GameObject keep)
    {
        foreach (HeroController other in FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == null || other.gameObject == keep)
                continue;
            other.enabled = false;
            other.gameObject.SetActive(false);
        }
    }

    static void SnapHeroToGround(Transform hero)
    {
        Vector3 origin = hero.position + Vector3.up * 40f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return;

        var cc = hero.GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null)
            cc.enabled = false;
        hero.position = hit.point;
        Physics.SyncTransforms();
        if (cc != null)
            cc.enabled = wasEnabled;
    }

    static void DisableLegacyInputShowcases()
    {
        // Huscarl demo uses old Input Manager and throws every frame under Input System.
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null)
                continue;
            System.Type type = mb.GetType();
            if (type.Name == "CombatShowcaseController")
                mb.enabled = false;
        }
    }

    HeroController SetupHero(
        GameObject heroObject,
        CharacterController designedBody,
        HeroController designedHero,
        float designedHeroHeight)
    {
        if (!heroObject.CompareTag("Player"))
        {
            try
            {
                heroObject.tag = "Player";
            }
            catch (UnityException)
            {
            }
        }

        var controller = heroObject.GetComponent<CharacterController>();
        if (controller == null)
            controller = heroObject.AddComponent<CharacterController>();
        if (designedBody != null && designedBody != controller)
            CopyController(designedBody, controller);
        else
            CharacterBodyFit.Apply(controller, heroObject.transform);
        controller.enabled = true;

        var animator = heroObject.GetComponent<Animator>();
        if (animator == null)
            animator = heroObject.AddComponent<Animator>();
        if (heroAnimator != null)
            animator.runtimeAnimatorController = heroAnimator;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.enabled = true;
        if (animator.avatar == null || !animator.avatar.isValid)
            Debug.LogWarning("GameplayBootstrap: hero2 Avatar 无效，动画可能无法播放。");

        var hero = heroObject.GetComponent<HeroController>();
        if (hero == null)
            hero = heroObject.AddComponent<HeroController>();
        hero.CopyTuningFrom(designedHero);
        hero.LockMotionToWorldHeight(designedHeroHeight);
        hero.SetMoveSpeeds(replacedWalkSpeed, replacedRunSpeed);
        hero.enabled = true;
        return hero;
    }

    static void MatchWorldHeight(Transform hero, float targetHeight)
    {
        if (hero == null || targetHeight < 0.2f)
            return;

        float current = 0f;
        if (CharacterBodyFit.TryMeasureWorldBounds(hero, out Bounds bounds))
            current = bounds.size.y;
        if (current < 0.2f)
            current = 1.8f;

        hero.localScale *= targetHeight / current;
    }

    static void CopyController(CharacterController from, CharacterController to)
    {
        to.height = from.height;
        to.radius = from.radius;
        to.center = from.center;
        to.skinWidth = from.skinWidth;
        to.minMoveDistance = from.minMoveDistance;
        to.slopeLimit = from.slopeLimit;
        to.stepOffset = from.stepOffset;
    }

    void SetupCamera(
        Transform hero,
        Vector3 designedCameraPos,
        Quaternion designedCameraRot,
        Vector3 designedHeroPos,
        float designedFov,
        float designedNear,
        float designedFar)
    {
        Camera main = Camera.main;
        if (main == null)
        {
            Debug.LogError("GameplayBootstrap: 找不到 Main Camera。");
            return;
        }

        main.fieldOfView = designedFov;
        main.nearClipPlane = designedNear;
        main.farClipPlane = designedFar;

        var follow = main.GetComponent<HeroFollowCamera>();
        if (follow == null)
            follow = main.gameObject.AddComponent<HeroFollowCamera>();

        Vector3 offset = designedCameraPos - designedHeroPos;
        main.transform.SetPositionAndRotation(hero.position + offset, designedCameraRot);
        follow.Bind(hero, offset, designedCameraRot);
    }

    void SetupHud(HeroController hero)
    {
        GameHud hud = FindAnyObjectByType<GameHud>();
        if (hud == null)
            hud = GameHud.Create();
        if (hud.Joystick != null)
            hero.SetJoystick(hud.Joystick);
        hud.BindHero(hero);
        if (hud.PunchButton != null)
        {
            hero.SetPunchButton(hud.PunchButton);
            hud.PunchButton.Pressed += hero.Punch;
        }
        if (hud.JumpButton != null)
            hud.JumpButton.Pressed += hero.Jump;
    }

    void SetupRoosterSpawner(Transform hero)
    {
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null)
            spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
        GameObject rooster = GameObject.Find("rooster");
        spawner.Initialize(rooster, hero);
    }

    void SetupWildlife()
    {
        GameObject rabbit = GameObject.Find("rabbit");
        GameObject eagle = GameObject.Find("Eagle");
        if (rabbit == null)
            return;

        var hunt = rabbit.GetComponent<RabbitEagleHunt>();
        if (hunt == null && rabbit.GetComponent<CharacterActionPlayer>() != null)
            hunt = rabbit.AddComponent<RabbitEagleHunt>();
        if (hunt != null)
            hunt.BindEagle(eagle);
    }
}
