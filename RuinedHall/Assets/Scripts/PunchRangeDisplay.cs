using UnityEngine;

// 出拳范围可视化：地面扇形或内外圈，用来调出手距离。
public sealed class PunchRangeDisplay : MonoBehaviour
{
    const int ArcSegments = 20;
    const int CircleSegments = 48;
    const float GroundLift = 0.06f;

    LineRenderer _ring;
    LineRenderer _outer;
    MeshFilter _fill;
    MeshRenderer _fillRenderer;
    Mesh _sector;
    Material _fillMat;
    Material _lineMat;
    Material _outerMat;
    TextMesh _innerLabel;
    TextMesh _outerLabel;

    // 创建独立的出拳范围显示物体。
    public static PunchRangeDisplay Create()
    {
        var go = new GameObject("PunchRange");
        return go.AddComponent<PunchRangeDisplay>();
    }

    // 准备扇形填充、内外圈线与半透明材质。
    void Awake()
    {
        _lineMat = MakeColorMaterial(new Color(1f, 0.82f, 0.2f, 0.95f));
        _outerMat = MakeColorMaterial(new Color(0.95f, 0.32f, 0.22f, 0.9f));
        _fillMat = MakeColorMaterial(new Color(1f, 0.72f, 0.15f, 0.22f));
        _ring = CreateLine("Arc", 0.05f, _lineMat);
        _ring.loop = true;
        _ring.positionCount = ArcSegments + 2;
        _outer = CreateLine("Outer", 0.045f, _outerMat);
        _outer.loop = true;
        _outer.positionCount = CircleSegments;

        var fillGo = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
        fillGo.transform.SetParent(transform, false);
        _fill = fillGo.GetComponent<MeshFilter>();
        _sector = new Mesh { name = "PunchRangeSector" };
        _fill.sharedMesh = _sector;
        _fillRenderer = fillGo.GetComponent<MeshRenderer>();
        _fillRenderer.sharedMaterial = _fillMat;
        _fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _fillRenderer.receiveShadows = false;
        Hide();
    }

    // 关掉所有线、填充和距离文字。
    public void Hide()
    {
        if (_ring != null)
            _ring.enabled = false;
        if (_outer != null)
            _outer.enabled = false;
        if (_fillRenderer != null)
            _fillRenderer.enabled = false;
        if (_innerLabel != null)
            _innerLabel.gameObject.SetActive(false);
        if (_outerLabel != null)
            _outerLabel.gameObject.SetActive(false);
    }

    // 在脚底画朝前的出拳扇形；命中结果用绿 / 红区分。
    public void Show(
        Vector3 feet,
        Vector3 forward,
        float radius,
        float arcDegrees,
        bool hitResolved,
        bool connected)
    {
        float y = feet.y + GroundLift;
        Vector3 origin = new Vector3(feet.x, y, feet.z);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Color ring = hitResolved
            ? (connected ? new Color(0.28f, 0.92f, 0.38f, 0.95f) : new Color(0.95f, 0.28f, 0.22f, 0.95f))
            : new Color(1f, 0.82f, 0.2f, 0.95f);
        Color fill = ring;
        fill.a = hitResolved ? 0.32f : 0.2f;
        SetMaterialColor(_lineMat, ring);
        SetMaterialColor(_fillMat, fill);

        if (_outer != null)
            _outer.enabled = false;
        if (_innerLabel != null)
            _innerLabel.gameObject.SetActive(false);
        if (_outerLabel != null)
            _outerLabel.gameObject.SetActive(false);
        _ring.enabled = true;
        _fillRenderer.enabled = true;

        float half = arcDegrees * 0.5f;
        _ring.SetPosition(0, origin);
        for (int i = 0; i <= ArcSegments; i++)
        {
            float t = i / (float)ArcSegments;
            float angle = Mathf.Lerp(-half, half, t);
            _ring.SetPosition(i + 1, origin + Quaternion.AngleAxis(angle, Vector3.up) * (forward * radius));
        }

        BuildSector(origin, forward, radius, half);
    }

    // 画出手圈和打中圈，并标上半径文字。
    public void ShowRings(
        Vector3 feet,
        float innerRadius,
        float outerRadius,
        bool attacking,
        bool hitApplied,
        string innerName = "出手",
        string outerName = "打中")
    {
        float y = feet.y + GroundLift;
        Vector3 origin = new Vector3(feet.x, y, feet.z);
        Color inner = attacking
            ? (hitApplied ? new Color(0.95f, 0.28f, 0.22f, 0.95f) : new Color(1f, 0.55f, 0.18f, 0.95f))
            : new Color(0.92f, 0.22f, 0.2f, 0.88f);
        Color outer = new Color(1f, 0.42f, 0.28f, 0.7f);
        Color fill = inner;
        fill.a = attacking ? 0.2f : 0.1f;

        SetMaterialColor(_lineMat, inner);
        SetMaterialColor(_outerMat, outer);
        SetMaterialColor(_fillMat, fill);

        _ring.enabled = true;
        _outer.enabled = true;
        _fillRenderer.enabled = true;
        DrawCircle(_ring, origin, innerRadius, CircleSegments);
        DrawCircle(_outer, origin, Mathf.Max(outerRadius, innerRadius), CircleSegments);
        BuildDisc(origin, innerRadius);
        PlaceLabel(ref _innerLabel, origin + Vector3.right * innerRadius, innerName + " " + innerRadius.ToString("0.0"));
        PlaceLabel(ref _outerLabel, origin + Vector3.right * Mathf.Max(outerRadius, innerRadius), outerName + " " + Mathf.Max(outerRadius, innerRadius).ToString("0.0"));
    }

    // 在世界坐标放一段面向相机的半径标签。
    void PlaceLabel(ref TextMesh label, Vector3 world, string text)
    {
        if (label == null)
        {
            var go = new GameObject("RangeLabel");
            go.transform.SetParent(transform, false);
            label = go.AddComponent<TextMesh>();
            label.fontSize = 48;
            label.characterSize = 0.06f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
        }

        label.gameObject.SetActive(true);
        label.text = text;
        label.transform.position = world + Vector3.up * 0.02f;
        if (Camera.main != null)
            label.transform.rotation = Camera.main.transform.rotation;
    }

    // 用折线画一个水平圆。
    static void DrawCircle(LineRenderer line, Vector3 origin, float radius, int segments)
    {
        if (line.positionCount != segments)
            line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    // 生成扇形填充网格。
    void BuildSector(Vector3 origin, Vector3 forward, float radius, float halfDegrees)
    {
        int count = ArcSegments + 2;
        var verts = new Vector3[count];
        var tris = new int[ArcSegments * 3];
        verts[0] = origin;
        for (int i = 0; i <= ArcSegments; i++)
        {
            float t = i / (float)ArcSegments;
            float angle = Mathf.Lerp(-halfDegrees, halfDegrees, t);
            verts[i + 1] = origin + Quaternion.AngleAxis(angle, Vector3.up) * (forward * radius);
            if (i == ArcSegments)
                continue;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        _sector.Clear();
        _sector.vertices = verts;
        _sector.triangles = tris;
        _sector.RecalculateNormals();
        _sector.RecalculateBounds();
        _fill.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _fill.transform.localScale = Vector3.one;
    }

    // 生成实心圆盘填充网格。
    void BuildDisc(Vector3 origin, float radius)
    {
        var verts = new Vector3[CircleSegments + 1];
        var tris = new int[CircleSegments * 3];
        verts[0] = origin;
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegments;
            verts[i + 1] = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 1 < CircleSegments ? i + 2 : 1;
        }

        _sector.Clear();
        _sector.vertices = verts;
        _sector.triangles = tris;
        _sector.RecalculateNormals();
        _sector.RecalculateBounds();
        _fill.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _fill.transform.localScale = Vector3.one;
    }

    // 创建一条世界空间折线。
    LineRenderer CreateLine(string name, float width, Material material)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(transform, false);
        var line = go.GetComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        return line;
    }

    // 找无光照 Shader 并做成双面彩色材质。
    static Material MakeColorMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader) { color = color };
        mat.SetInt("_Cull", 0);
        SetMaterialColor(mat, color);
        return mat;
    }

    // 同时写 _Color 和 _BaseColor，兼容不同 Shader。
    static void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
    }

    // 销毁运行时创建的材质和网格。
    void OnDestroy()
    {
        if (_fillMat != null)
            Destroy(_fillMat);
        if (_lineMat != null)
            Destroy(_lineMat);
        if (_outerMat != null)
            Destroy(_outerMat);
        if (_sector != null)
            Destroy(_sector);
    }
}
