using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class TestBodyBootstrap : MonoBehaviour
{
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

    static void EnsureUrpCamera(Camera cam)
    {
        if (cam == null)
            return;
        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
    }

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

public class SidekickTurntable : MonoBehaviour
{
    [SerializeField] float degreesPerPixel = 0.28f;

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
