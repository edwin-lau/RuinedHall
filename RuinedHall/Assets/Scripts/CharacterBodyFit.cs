using UnityEngine;

/// <summary>
/// 按网格包围盒适配 CharacterController，并把脚贴到地面或抬出地面。
/// </summary>
public static class CharacterBodyFit
{
    /// <summary>返回角色在世界空间中的身高。</summary>
    public static float WorldHeight(Transform root)
    {
        if (root == null)
            return 1.8f;

        var controller = root.GetComponent<CharacterController>();
        if (controller != null && controller.height > 0.01f)
            return Mathf.Max(0.2f, controller.height * Mathf.Abs(root.lossyScale.y));

        if (!TryMeasureWorldBounds(root, out Bounds bounds))
            return 1.8f;
        return Mathf.Max(0.2f, bounds.size.y);
    }

    /// <summary>按网格包围盒写入 CharacterController 的身高、半径与中心。</summary>
    public static void Apply(CharacterController controller, Transform root)
    {
        if (controller == null || root == null)
            return;
        if (!TryMeasureWorldBounds(root, out Bounds bounds))
            return;

        float sy = Mathf.Max(0.0001f, Mathf.Abs(root.lossyScale.y));
        float localHeight = Mathf.Max(0.4f, bounds.size.y / sy);
        float localBottom = (bounds.min.y - root.position.y) / sy;
        float radius = Mathf.Clamp(localHeight * 0.16f, 0.06f, localHeight * 0.28f);

        // 用局部身高写胶囊体，避免缩放把碰撞体拉飞。
        controller.height = localHeight;
        controller.radius = radius;
        controller.center = new Vector3(0f, localBottom + localHeight * 0.5f, 0f);
        controller.skinWidth = Mathf.Clamp(radius * 0.12f, 0.02f, 0.08f);
        controller.minMoveDistance = 0f;
        controller.slopeLimit = 45f;
        controller.stepOffset = Mathf.Min(localHeight * 0.18f, radius * 1.8f);
    }

    /// <summary>测量所有可见网格的世界包围盒。</summary>
    public static bool TryMeasureWorldBounds(Transform root, out Bounds bounds)
    {
        return TryMeasureBounds(root, false, out bounds);
    }

    /// <summary>优先用蒙皮网格测包围盒，失败再退回全部网格。</summary>
    public static bool TryMeasureBodyBounds(Transform root, out Bounds bounds)
    {
        if (TryMeasureBounds(root, true, out bounds))
            return true;
        return TryMeasureBounds(root, false, out bounds);
    }

    // 合并子网格包围盒；skinnedOnly 时只收蒙皮网格。
    static bool TryMeasureBounds(Transform root, bool skinnedOnly, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer is ParticleSystemRenderer)
                continue;
            if (skinnedOnly && renderer is not SkinnedMeshRenderer)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found && bounds.size.y > 0.05f;
    }

    /// <summary>把角色脚底对齐到地面，必要时再按包围盒抬高。</summary>
    public static void SnapFeetToGround(Transform root)
    {
        if (root == null)
            return;

        var animator = root.GetComponent<Animator>();
        if (animator != null && animator.enabled)
            animator.Update(0f);

        if (!TryHitGround(root.position, root, out Vector3 ground))
            return;

        var controller = root.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null)
            controller.enabled = false;

        // 先按脚点对齐地面，再按包围盒补一次，避免网格仍陷地。
        float footY = LowestContactY(root);
        root.position += Vector3.up * (ground.y - footY);

        Physics.SyncTransforms();
        if (TryMeasureBodyBounds(root, out Bounds after) && after.min.y < ground.y)
        {
            root.position += Vector3.up * (ground.y - after.min.y);
            Physics.SyncTransforms();
        }

        if (controller != null)
            controller.enabled = wasEnabled;
    }

    /// <summary>脚骨与身体包围盒中更低的接触高度。</summary>
    public static float LowestContactY(Transform root)
    {
        float y = LowestFootBoneY(root);
        if (TryMeasureBodyBounds(root, out Bounds bounds))
            y = Mathf.Min(y, bounds.min.y);
        if (y < float.PositiveInfinity)
            return y;
        return root.position.y;
    }

    /// <summary>人形脚/脚趾骨骼与按名称匹配的脚点中的最低 Y。</summary>
    public static float LowestFootBoneY(Transform root)
    {
        float y = float.PositiveInfinity;
        var animator = root.GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            ConsiderBone(animator, HumanBodyBones.LeftToes, ref y);
            ConsiderBone(animator, HumanBodyBones.RightToes, ref y);
            ConsiderBone(animator, HumanBodyBones.LeftFoot, ref y);
            ConsiderBone(animator, HumanBodyBones.RightFoot, ref y);
        }

        y = Mathf.Min(y, LowestNamedFootY(root));
        return y;
    }

    /// <summary>若脚陷进地面则向上抬出。</summary>
    public static void LiftFeetOutOfGround(CharacterController controller, Transform root)
    {
        if (root == null)
            return;

        float footY = LowestFootBoneY(root);
        if (footY >= float.PositiveInfinity)
            return;
        if (!TryHitGround(root.position, root, out Vector3 ground))
            return;

        float bury = ground.y - footY;
        if (bury <= 0.02f)
            return;

        if (controller != null && controller.enabled)
            controller.Move(Vector3.up * bury);
        else
            root.position += Vector3.up * bury;
    }

    // 按 toe/foot/ankle 名称找最低脚点。
    static float LowestNamedFootY(Transform root)
    {
        float y = float.PositiveInfinity;
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child == null)
                continue;
            string name = child.name;
            if (name.IndexOf("toe", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("foot", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("ankle", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            y = Mathf.Min(y, child.position.y);
        }

        return y;
    }

    // 把指定人形骨骼的世界 Y 纳入最低值。
    static void ConsiderBone(Animator animator, HumanBodyBones bone, ref float y)
    {
        Transform t = animator.GetBoneTransform(bone);
        if (t != null)
            y = Mathf.Min(y, t.position.y);
    }

    /// <summary>向下射线找地面，忽略自身碰撞，失败再采样地形。</summary>
    public static bool TryHitGround(Vector3 xz, Transform ignore, out Vector3 point)
    {
        point = xz;
        // 从上往下打射线，取最高的非自身碰撞点当地面。
        RaycastHit[] hits = Physics.RaycastAll(
            xz + Vector3.up * 40f,
            Vector3.down,
            200f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;
            if (ignore != null && hit.collider.transform.IsChildOf(ignore))
                continue;
            if (hit.point.y <= bestY)
                continue;
            bestY = hit.point.y;
            point = hit.point;
            found = true;
        }

        if (found)
            return true;

        // 射线未命中时，用地形高度作为地面。
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 local = xz - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                continue;
            point = new Vector3(xz.x, terrain.SampleHeight(xz) + terrain.transform.position.y, xz.z);
            return true;
        }

        return false;
    }
}
