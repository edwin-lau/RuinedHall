using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// 换装预览场景引导：建衣柜、补地面灯光、对准相机并弹出 HUD。
public class TestBodyBootstrap : MonoBehaviour
{
    // 创建隔离预览衣柜，并补齐相机 / 地面 / 灯光。
    void Awake()
    {
        var host = new GameObject("SidekickPreview");
        host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var wardrobe = host.AddComponent<SidekickWardrobe>();
        wardrobe.IsolatePreview = true;
        host.AddComponent<SidekickTurntable>();

        EnsureUrpCamera(Camera.main);
        EnsureGround();
        BumpLight();
        StartCoroutine(InitWardrobe(wardrobe, host));
    }

    // 等衣柜初始化完成后对准角色并弹出换装 HUD。
    IEnumerator InitWardrobe(SidekickWardrobe wardrobe, GameObject character)
    {
        var task = wardrobe.Initialize();
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError("TestBodyBootstrap: Sidekick 零件库加载失败。\n" + task.Exception);
            FrameCamera(character);
            yield break;
        }

        if (!wardrobe.Ready)
        {
            Debug.LogError("TestBodyBootstrap: Sidekick 零件库没有就绪。");
            FrameCamera(character);
            yield break;
        }

        FrameCamera(character);
        SidekickWardrobeHud.Create(wardrobe);
    }

    // 给主相机补上 URP 附加数据。
    static void EnsureUrpCamera(Camera cam)
    {
        if (cam == null)
            return;
        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
    }

    // 按角色包围盒把相机拉到正面合适距离。
    static void FrameCamera(GameObject character)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Bounds bounds = new Bounds(character.transform.position + Vector3.up, Vector3.one);
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled)
                continue;
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        Vector3 center = hasBounds ? bounds.center : new Vector3(0f, 1f, 0f);
        float radius = hasBounds ? Mathf.Max(0.9f, bounds.extents.magnitude) : 1.1f;
        cam.transform.position = center + new Vector3(0f, 0.05f, Mathf.Max(2.8f, radius * 2.15f));
        cam.transform.LookAt(center);
        cam.fieldOfView = 36f;
        cam.nearClipPlane = 0.05f;
        cam.backgroundColor = new Color(0.18f, 0.2f, 0.24f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    // 没有地面时铺一块深色 URP 平面。
    static void EnsureGround()
    {
        if (GameObject.Find("TestBodyGround") != null)
            return;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "TestBodyGround";
        ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f);

        var renderer = ground.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.18f, 1f));
            renderer.sharedMaterial = mat;
        }
    }

    // 把方向光提亮并补上 URP 灯光数据。
    static void BumpLight()
    {
        Light light = FindFirstObjectByType<Light>();
        if (light == null)
            return;
        if (light.type == LightType.Directional)
            light.intensity = Mathf.Max(light.intensity, 1.8f);
        if (light.GetComponent<UniversalAdditionalLightData>() == null)
            light.gameObject.AddComponent<UniversalAdditionalLightData>();
    }
}

// 鼠标拖拽水平旋转预览角色；指针在 UI 上时不转。
public class SidekickTurntable : MonoBehaviour
{
    [SerializeField] float degreesPerPixel = 0.28f;

    // 按鼠标水平位移绕世界 Y 轴旋转。
    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed)
            return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        float yaw = -mouse.delta.ReadValue().x * degreesPerPixel;
        transform.Rotate(0f, yaw, 0f, Space.World);
    }
}
