using UnityEngine;

public class GameplayBootstrap : MonoBehaviour
{
    [SerializeField] RuntimeAnimatorController heroAnimator;
    [SerializeField] string heroObjectName = "hero";

    void Awake()
    {
        GameObject heroObject = GameObject.Find(heroObjectName);
        if (heroObject == null)
        {
            Debug.LogError("GameplayBootstrap: 场景里找不到名为 hero 的主角。");
            return;
        }

        HeroController hero = SetupHero(heroObject);
        SetupCamera(heroObject.transform);
        SetupHud(hero);
    }

    HeroController SetupHero(GameObject heroObject)
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
        controller.height = 1.8f;
        controller.radius = 0.28f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.skinWidth = 0.04f;
        controller.minMoveDistance = 0f;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.35f;

        var animator = heroObject.GetComponent<Animator>();
        if (animator == null)
            animator = heroObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = heroAnimator;
        animator.applyRootMotion = false;

        var hero = heroObject.GetComponent<HeroController>();
        if (hero == null)
            hero = heroObject.AddComponent<HeroController>();
        return hero;
    }

    void SetupCamera(Transform hero)
    {
        Camera main = Camera.main;
        if (main == null)
        {
            Debug.LogError("GameplayBootstrap: 找不到 Main Camera。");
            return;
        }

        var follow = main.GetComponent<HeroFollowCamera>();
        if (follow == null)
            follow = main.gameObject.AddComponent<HeroFollowCamera>();
        follow.SetTarget(hero);
    }

    void SetupHud(HeroController hero)
    {
        GameHud hud = GameHud.Create();
        hero.SetJoystick(hud.Joystick);
        hud.PunchButton.Pressed += hero.Punch;
        hud.JumpButton.Pressed += hero.Jump;
    }
}
