using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using Synty.SidekickCharacters.Enums;
using Synty.SidekickCharacters.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

public sealed class WardrobePart
{
    public string Slot;
    public string Name;
    public string Label;
}

public class SidekickWardrobe : MonoBehaviour
{
    const string CharacterName = "Sidekick Character";
    static readonly Regex OutfitCode = new Regex(@"^SK_([A-Z]+)_([A-Z]+)_(\d+)_", RegexOptions.Compiled);

    static readonly Dictionary<string, string> SlotLabels = new Dictionary<string, string>
    {
        { "01HEAD", "头" },
        { "02HAIR", "头发" },
        { "03EBRL", "左眉" },
        { "04EBRR", "右眉" },
        { "05EYEL", "左眼" },
        { "06EYER", "右眼" },
        { "07EARL", "左耳" },
        { "08EARR", "右耳" },
        { "09FCHR", "胡须" },
        { "10TORS", "躯干" },
        { "11AUPL", "左上臂" },
        { "12AUPR", "右上臂" },
        { "13ALWL", "左下臂" },
        { "14ALWR", "右下臂" },
        { "15HNDL", "左手" },
        { "16HNDR", "右手" },
        { "17HIPS", "髋" },
        { "18LEGL", "左腿" },
        { "19LEGR", "右腿" },
        { "20FOTL", "左脚" },
        { "21FOTR", "右脚" },
        { "22AHED", "头饰" },
        { "23AFAC", "面饰" },
        { "24ABAC", "背部" },
        { "25AHPF", "髋前挂件" },
        { "26AHPB", "髋后挂件" },
        { "27AHPL", "左髋挂件" },
        { "28AHPR", "右髋挂件" },
        { "29ASHL", "左肩" },
        { "30ASHR", "右肩" },
        { "31AEBL", "左肘" },
        { "32AEBR", "右肘" },
        { "33AKNL", "左膝" },
        { "34AKNR", "右膝" },
        { "35NOSE", "鼻子" },
        { "36TETH", "牙" },
        { "37TONG", "舌" },
        { "38WRAP", "包裹" },
    };

    static readonly string[] DefaultPartNames =
    {
        "SK_HUMN_BASE_02_01HEAD_HU01",
        "SK_HUMN_BASE_01_37TONG_HU01",
        "SK_HUMN_BASE_03_35NOSE_HU01",
        "SK_HUMN_BASE_03_02HAIR_HU01",
        "SK_HUMN_BASE_01_10TORS_HU01",
        "SK_HUMN_BASE_03_03EBRL_HU01",
        "SK_HUMN_BASE_01_11AUPL_HU01",
        "SK_HUMN_BASE_01_12AUPR_HU01",
        "SK_HUMN_BASE_01_13ALWL_HU01",
        "SK_HUMN_BASE_01_14ALWR_HU01",
        "SK_HUMN_BASE_01_15HNDL_HU01",
        "SK_HUMN_BASE_01_16HNDR_HU01",
        "SK_HUMN_BASE_01_17HIPS_HU01",
        "SK_HUMN_BASE_01_18LEGL_HU01",
        "SK_HUMN_BASE_01_19LEGR_HU01",
        "SK_HUMN_BASE_01_20FOTL_HU01",
        "SK_HUMN_BASE_01_21FOTR_HU01",
        "SK_HUMN_BASE_01_08EARR_HU01",
        "SK_HUMN_BASE_03_04EBRR_HU01",
        "SK_HUMN_BASE_01_06EYER_HU01",
        "SK_HUMN_BASE_01_05EYEL_HU01",
        "SK_HUMN_BASE_01_07EARL_HU01",
        "SK_HUMN_BASE_01_36TETH_HU01",
    };

    static readonly HashSet<string> OptionalSlots = new HashSet<string>
    {
        "09FCHR", "38WRAP",
        "22AHED", "23AFAC", "24ABAC",
        "25AHPF", "26AHPB", "27AHPL", "28AHPR",
        "29ASHL", "30ASHR", "31AEBL", "32AEBR", "33AKNL", "34AKNR",
    };

    public static readonly string[] UiSlotOrder =
    {
        "10TORS", "17HIPS",
        "11AUPL", "12AUPR", "13ALWL", "14ALWR", "15HNDL", "16HNDR",
        "18LEGL", "19LEGR", "20FOTL", "21FOTR",
        "02HAIR", "01HEAD", "09FCHR",
        "03EBRL", "04EBRR", "05EYEL", "06EYER", "07EARL", "08EARR",
        "35NOSE", "36TETH", "37TONG", "38WRAP",
        "22AHED", "23AFAC", "24ABAC",
        "25AHPF", "26AHPB", "27AHPL", "28AHPR",
        "29ASHL", "30ASHR", "31AEBL", "32AEBR", "33AKNL", "34AKNR",
    };

    public event Action Changed;

    readonly Dictionary<string, List<WardrobePart>> _partsBySlot = new Dictionary<string, List<WardrobePart>>();
    readonly Dictionary<string, string> _defaults = new Dictionary<string, string>();
    readonly Dictionary<string, string> _equipped = new Dictionary<string, string>();
    readonly Dictionary<string, SidekickPart> _dbParts = new Dictionary<string, SidekickPart>();
    readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

    DatabaseManager _db;
    SidekickRuntime _runtime;
    RuntimeAnimatorController _animatorController;
    GameObject _character;
    float _skinny = 22f;
    float _heavy;
    float _muscles = 28f;
    float _faceBlend;

    public IReadOnlyDictionary<string, string> Equipped => _equipped;
    public bool Ready { get; private set; }
    public bool IsolatePreview { get; set; }

    public static string SlotLabel(string slot)
    {
        return SlotLabels.TryGetValue(slot, out string label) ? label : slot;
    }

    public IReadOnlyList<WardrobePart> PartsForSlot(string slot)
    {
        return _partsBySlot.TryGetValue(slot, out List<WardrobePart> parts)
            ? parts
            : Array.Empty<WardrobePart>();
    }

    public bool IsDefault(string slot)
    {
        _defaults.TryGetValue(slot, out string def);
        _equipped.TryGetValue(slot, out string cur);
        return string.Equals(def ?? "", cur ?? "", StringComparison.Ordinal);
    }

    public async Task Initialize(SidekickLook look = null)
    {
        GameObject model = Resources.Load<GameObject>("Meshes/SK_BaseModel");
        Material material = Resources.Load<Material>("Materials/M_BaseMaterial");
        if (model == null || material == null)
        {
            Debug.LogError("SidekickWardrobe: 缺少 SK_BaseModel 或 M_BaseMaterial。");
            return;
        }

        _animatorController = Resources.Load<RuntimeAnimatorController>("characters/hero2/Hero2");
        _db = new DatabaseManager();
        _runtime = new SidekickRuntime(model, material, _animatorController, _db);
        _runtime.ForceAssignedBaseModel = true;
        await SidekickRuntime.PopulateToolData(_runtime);

        if (IsolatePreview)
        {
            CachePrefabs();
            BuildCatalog();
            if (_dbParts.Count == 0)
            {
                Debug.LogError("SidekickWardrobe: 零件库是空的，检查 Side_Kick_Data.db 和 Resources/Meshes。");
                return;
            }
        }

        RememberDefaults();
        SidekickLook saved = look ?? SidekickLook.Load();
        if (saved != null)
            ApplyLook(saved, false);
        EnsureEquippedPrefabs();
        if (_dbParts.Count == 0)
        {
            Debug.LogError("SidekickWardrobe: 零件库是空的，检查 Side_Kick_Data.db 和 Resources/Meshes。");
            return;
        }

        RebuildCharacter();
        Ready = true;
        Changed?.Invoke();
    }

    public SidekickLook CaptureLook()
    {
        return SidekickLook.From(_equipped, _skinny, _heavy, _muscles, _faceBlend);
    }

    public void SaveLookForGame()
    {
        CaptureLook().Save();
    }

    public void ApplyLook(SidekickLook look, bool rebuild = true)
    {
        if (look == null)
            return;

        _skinny = look.skinny;
        _heavy = look.heavy;
        _muscles = look.muscles;
        _faceBlend = look.faceBlend;
        look.WriteTo(_equipped);
        if (rebuild)
        {
            RebuildCharacter();
            Changed?.Invoke();
        }
    }

    public GameObject DetachCharacter()
    {
        GameObject character = _character;
        _character = null;
        if (character != null)
            character.transform.SetParent(null, true);
        return character;
    }

    public static async Task<GameObject> CreatePlayable(SidekickLook look)
    {
        var host = new GameObject("SidekickBuild");
        var wardrobe = host.AddComponent<SidekickWardrobe>();
        await wardrobe.Initialize(look);
        GameObject character = wardrobe.DetachCharacter();
        UnityEngine.Object.Destroy(host);
        return character;
    }

    public void Toggle(string slot, string partName)
    {
        _equipped.TryGetValue(slot, out string current);
        if (string.Equals(current, partName, StringComparison.Ordinal))
            Restore(slot);
        else
            Equip(slot, partName);
    }

    public void Restore(string slot)
    {
        if (_defaults.TryGetValue(slot, out string def) && !string.IsNullOrEmpty(def))
            _equipped[slot] = def;
        else
            _equipped[slot] = "";
        RebuildCharacter();
        Changed?.Invoke();
    }

    public void RestoreAll()
    {
        RememberDefaults();
        RebuildCharacter();
        Changed?.Invoke();
    }

    public void EquipSet(string namePrefix)
    {
        foreach (KeyValuePair<string, List<WardrobePart>> pair in _partsBySlot)
        {
            WardrobePart match = null;
            for (int i = 0; i < pair.Value.Count; i++)
            {
                if (pair.Value[i].Name.StartsWith(namePrefix, StringComparison.Ordinal))
                    match = pair.Value[i];
            }

            if (match != null)
                _equipped[pair.Key] = match.Name;
        }

        RebuildCharacter();
        Changed?.Invoke();
    }

    public void Equip(string slot, string partName)
    {
        _equipped[slot] = partName ?? "";
        RebuildCharacter();
        Changed?.Invoke();
    }

    void CachePrefabs()
    {
        _prefabs.Clear();
        GameObject[] loaded = Resources.LoadAll<GameObject>("Meshes");
        for (int i = 0; i < loaded.Length; i++)
        {
            GameObject prefab = loaded[i];
            if (prefab == null || !prefab.name.StartsWith("SK_", StringComparison.Ordinal))
                continue;
            if (prefab.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                continue;
            _prefabs[prefab.name] = prefab;
        }
    }

    void BuildCatalog()
    {
        _partsBySlot.Clear();
        _dbParts.Clear();

        if (_runtime?.MappedPartDictionary == null)
            return;

        foreach (KeyValuePair<CharacterPartType, Dictionary<string, SidekickPart>> typeEntry in _runtime.MappedPartDictionary)
        {
            string slot = CharacterPartTypeUtils.GetPartTypeString(typeEntry.Key);
            if (string.IsNullOrEmpty(slot))
                continue;

            foreach (KeyValuePair<string, SidekickPart> partEntry in typeEntry.Value)
            {
                SidekickPart dbPart = partEntry.Value;
                if (dbPart == null || ResolvePrefab(dbPart) == null)
                    continue;

                _dbParts[dbPart.Name] = dbPart;
                if (!_partsBySlot.TryGetValue(slot, out List<WardrobePart> list))
                {
                    list = new List<WardrobePart>();
                    _partsBySlot[slot] = list;
                }

                list.Add(new WardrobePart
                {
                    Slot = slot,
                    Name = dbPart.Name,
                    Label = FormatPartLabel(dbPart.Name),
                });
            }
        }

        foreach (List<WardrobePart> list in _partsBySlot.Values)
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
    }

    void RememberDefaults()
    {
        _defaults.Clear();
        _equipped.Clear();

        for (int i = 0; i < DefaultPartNames.Length; i++)
        {
            string name = DefaultPartNames[i];
            if (!TryRegisterPart(name))
                continue;

            string slot = SlotOf(name);
            if (string.IsNullOrEmpty(slot))
                continue;
            _defaults[slot] = name;
            _equipped[slot] = name;
        }

        foreach (KeyValuePair<string, List<WardrobePart>> pair in _partsBySlot)
        {
            if (_equipped.ContainsKey(pair.Key) || OptionalSlots.Contains(pair.Key))
                continue;

            string fallback = FirstBasePart(pair.Value);
            if (string.IsNullOrEmpty(fallback))
                continue;
            _defaults[pair.Key] = fallback;
            _equipped[pair.Key] = fallback;
        }

        _defaults["09FCHR"] = "";
        _equipped["09FCHR"] = "";
    }

    void EnsureEquippedPrefabs()
    {
        foreach (KeyValuePair<string, string> pair in _equipped)
            TryRegisterPart(pair.Value);
    }

    bool TryRegisterPart(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (_dbParts.ContainsKey(name))
            return true;
        if (_runtime?.MappedPartDictionary == null)
            return false;

        foreach (KeyValuePair<CharacterPartType, Dictionary<string, SidekickPart>> typeEntry in _runtime.MappedPartDictionary)
        {
            Dictionary<string, SidekickPart> byName = typeEntry.Value;
            if (byName == null)
                continue;

            SidekickPart part;
            if (!byName.TryGetValue(name, out part))
            {
                part = null;
                foreach (KeyValuePair<string, SidekickPart> entry in byName)
                {
                    if (entry.Value != null && entry.Value.Name == name)
                    {
                        part = entry.Value;
                        break;
                    }
                }
            }

            if (part == null || ResolvePrefab(part) == null)
                continue;

            _dbParts[part.Name] = part;
            return true;
        }

        return false;
    }

    public float Skinny => _skinny;
    public float Heavy => _heavy;
    public float Muscles => _muscles;
    public float FaceBlend => _faceBlend;

    public void SetBody(float skinny, float heavy, float muscles)
    {
        _skinny = Mathf.Clamp(skinny, 0f, 100f);
        _heavy = Mathf.Clamp(heavy, 0f, 100f);
        _muscles = Mathf.Clamp(muscles, -100f, 100f);
        RebuildCharacter();
        Changed?.Invoke();
    }

    public void SetFaceBlend(float value)
    {
        _faceBlend = Mathf.Clamp(value, -100f, 100f);
        RebuildCharacter();
        Changed?.Invoke();
    }

    public void EquipFace(string headName)
    {
        if (string.IsNullOrEmpty(headName))
            return;

        _equipped["01HEAD"] = headName;

        Match match = OutfitCode.Match(headName);
        if (match.Success)
        {
            string prefix = "SK_" + match.Groups[1].Value + "_" + match.Groups[2].Value + "_" + match.Groups[3].Value + "_";
            string[] faceSlots = { "35NOSE", "03EBRL", "04EBRR" };
            for (int i = 0; i < faceSlots.Length; i++)
            {
                IReadOnlyList<WardrobePart> parts = PartsForSlot(faceSlots[i]);
                for (int p = 0; p < parts.Count; p++)
                {
                    if (!parts[p].Name.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    _equipped[faceSlots[i]] = parts[p].Name;
                    break;
                }
            }
        }

        RebuildCharacter();
        Changed?.Invoke();
    }

    void RebuildCharacter()
    {
        if (_runtime == null)
            return;

        var partsToUse = new List<SkinnedMeshRenderer>();
        foreach (KeyValuePair<string, string> pair in _equipped)
        {
            if (string.IsNullOrEmpty(pair.Value))
                continue;
            if (!_dbParts.TryGetValue(pair.Value, out SidekickPart dbPart))
                continue;

            GameObject prefab = ResolvePrefab(dbPart);
            if (prefab == null)
                continue;

            SkinnedMeshRenderer smr = FindPartRenderer(prefab, dbPart);
            if (smr != null && smr.sharedMesh != null)
                partsToUse.Add(smr);
        }

        if (partsToUse.Count == 0)
        {
            Debug.LogError("SidekickWardrobe: 没有可合成的部件。");
            return;
        }

        DestroyOldCharacter();
        if (IsolatePreview)
            DestroyBakedPresets();
        ApplyBodyShape();

        bool combine = !IsolatePreview;
        _character = _runtime.CreateCharacter(
            CharacterName,
            partsToUse,
            combine,
            IsolatePreview,
            null,
            IsolatePreview,
            IsolatePreview);
        if (_character == null)
            return;

        _character.transform.SetParent(transform, false);
        _character.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _character.transform.localScale = Vector3.one;

        if (IsolatePreview)
        {
            HashSet<SkinnedMeshRenderer> live = KeepOnlyRequestedParts(_character, partsToUse);
            HideForeignBodies(live);
            BindAnimator(_character);
            if (_character.GetComponent<SidekickMouthClosed>() == null)
                _character.AddComponent<SidekickMouthClosed>();
        }
        else
        {
            OptimizePlayableCharacter(_character);
            BindAnimator(_character);
            if (_character.GetComponent<SidekickJawLock>() == null)
                _character.AddComponent<SidekickJawLock>();
        }
    }

    static void OptimizePlayableCharacter(GameObject character)
    {
        Material playableMat = CreatePlayableLit(Resources.Load<Material>("Materials/M_BaseMaterial"));
        SkinnedMeshRenderer[] skins = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skins.Length; i++)
        {
            SkinnedMeshRenderer smr = skins[i];
            smr.updateWhenOffscreen = true;
            smr.skinnedMotionVectors = false;
            smr.quality = SkinQuality.Bone4;
            if (playableMat != null)
                smr.sharedMaterial = playableMat;
        }
    }

    static Material CreatePlayableLit(Material source)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return source;

        var material = new Material(lit);
        material.name = "SidekickPlayableLit";
        Texture color = null;
        if (source != null)
        {
            if (source.HasProperty("_ColorMap"))
                color = source.GetTexture("_ColorMap");
            if (color == null && source.HasProperty("_MainTex"))
                color = source.GetTexture("_MainTex");
        }

        if (color != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", color);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", color);
        }

        material.enableInstancing = true;
        return material;
    }

    void BindAnimator(GameObject character)
    {
        Animator animator = character.GetComponent<Animator>();
        if (animator == null)
            animator = character.AddComponent<Animator>();

        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.fireEvents = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (_animatorController != null)
            animator.runtimeAnimatorController = _animatorController;
        animator.Rebind();
        animator.Update(0f);
    }

    void ApplyBodyShape()
    {
        if (_runtime == null)
            return;
        _runtime.BodySizeSkinnyBlendValue = _skinny;
        _runtime.BodySizeHeavyBlendValue = _heavy;
        _runtime.MusclesBlendValue = _muscles;
        _runtime.BodyTypeBlendValue = _faceBlend;
    }

    void DestroyOldCharacter()
    {
        if (_character != null)
        {
            _character.SetActive(false);
            Destroy(_character);
            _character = null;
        }
    }

    static void DestroyBakedPresets()
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i].gameObject;
            if (go == null || go.transform.parent != null)
                continue;
            if (!IsBakedPresetName(go.name))
                continue;
            go.SetActive(false);
            Destroy(go);
        }
    }

    static bool IsBakedPresetName(string name)
    {
        return name == "HumanSpecies_01"
            || name == "HumanSpecies_01(Clone)"
            || name == "Prefab Character"
            || name.StartsWith("Starter_0", StringComparison.Ordinal)
            || name.StartsWith("Example_", StringComparison.Ordinal);
    }

    static HashSet<SkinnedMeshRenderer> KeepOnlyRequestedParts(GameObject character, List<SkinnedMeshRenderer> requested)
    {
        var keepNames = new HashSet<string>();
        for (int i = 0; i < requested.Count; i++)
        {
            if (requested[i] != null)
                keepNames.Add(requested[i].name);
        }

        var chosen = new Dictionary<string, SkinnedMeshRenderer>();
        for (int i = 0; i < character.transform.childCount; i++)
        {
            Transform child = character.transform.GetChild(i);
            SkinnedMeshRenderer smr = child.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || !keepNames.Contains(smr.name))
                continue;
            chosen[smr.name] = smr;
        }

        var live = new HashSet<SkinnedMeshRenderer>();
        SkinnedMeshRenderer[] all = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (chosen.TryGetValue(all[i].name, out SkinnedMeshRenderer pick) && pick == all[i])
            {
                all[i].enabled = true;
                live.Add(all[i]);
            }
            else
            {
                all[i].enabled = false;
            }
        }

        return live;
    }

    static void HideForeignBodies(HashSet<SkinnedMeshRenderer> live)
    {
        SkinnedMeshRenderer[] all = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (live != null && live.Contains(all[i]))
                continue;
            all[i].enabled = false;
        }
    }

    static SkinnedMeshRenderer FindPartRenderer(GameObject prefab, SidekickPart part)
    {
        SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0)
            return null;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name == part.Name || renderers[i].gameObject.name == part.Name)
                return renderers[i];
        }

        string code = CharacterPartTypeUtils.GetPartTypeString(part.Type);
        for (int i = 0; i < renderers.Length; i++)
        {
            string name = renderers[i].name;
            if (name.StartsWith("SK_SPEC_", StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrEmpty(code) && name.IndexOf(code, StringComparison.Ordinal) >= 0)
                return renderers[i];
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].name.StartsWith("SK_SPEC_", StringComparison.Ordinal))
                return renderers[i];
        }

        return renderers[0];
    }

    GameObject ResolvePrefab(SidekickPart part)
    {
        if (part == null)
            return null;

        if (_prefabs.TryGetValue(part.Name, out GameObject cached) && cached != null)
            return cached;

        try
        {
            GameObject loaded = part.GetPartModel();
            if (loaded != null)
            {
                _prefabs[part.Name] = loaded;
                return loaded;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    static string SlotOf(string partName)
    {
        foreach (CharacterPartType type in Enum.GetValues(typeof(CharacterPartType)))
        {
            string code = CharacterPartTypeUtils.GetPartTypeString(type);
            if (!string.IsNullOrEmpty(code) && partName.IndexOf(code, StringComparison.Ordinal) >= 0)
                return code;
        }

        return "";
    }

    static string FirstBasePart(List<WardrobePart> parts)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].Name.IndexOf("_BASE_", StringComparison.Ordinal) >= 0)
                return parts[i].Name;
        }

        return "";
    }

    static string FormatPartLabel(string name)
    {
        Match match = OutfitCode.Match(name);
        if (!match.Success)
            return name;

        string family = match.Groups[1].Value + "_" + match.Groups[2].Value;
        string version = match.Groups[3].Value;
        switch (family)
        {
            case "HUMN_BASE":
                return "人体 " + version;
            case "FANT_KNGT":
                return "骑士 " + version;
            case "SCFI_CIVL":
                return "科幻 " + version;
            case "HORR_VILN":
                return "恐怖 " + version;
            default:
                return family + " " + version;
        }
    }
}

public class SidekickJawLock : MonoBehaviour
{
    Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void LateUpdate()
    {
        if (_animator != null && _animator.isHuman)
            _animator.SetBoneLocalRotation(HumanBodyBones.Jaw, Quaternion.identity);
    }
}

public class SidekickMouthClosed : MonoBehaviour
{
    struct ShapeBind
    {
        public SkinnedMeshRenderer Renderer;
        public int Index;
        public float Weight;
    }

    Animator _animator;
    Transform _jaw;
    Quaternion _closedJaw;
    ShapeBind[] _shapes = Array.Empty<ShapeBind>();

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _jaw = FindNamed(transform, "jaw");
        if (_jaw != null)
            _closedJaw = _jaw.localRotation;
        CacheShapes();
        ApplyCached();
    }

    void LateUpdate()
    {
        ApplyCached();
        if (_animator != null && _animator.isHuman)
            _animator.SetBoneLocalRotation(HumanBodyBones.Jaw, Quaternion.identity);
        if (_jaw != null)
            _jaw.localRotation = _closedJaw;
    }

    void CacheShapes()
    {
        var list = new List<ShapeBind>(8);
        SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>(false);
        for (int i = 0; i < skins.Length; i++)
        {
            Mesh mesh = skins[i].sharedMesh;
            if (mesh == null)
                continue;
            for (int s = 0; s < mesh.blendShapeCount; s++)
            {
                string name = mesh.GetBlendShapeName(s);
                float weight;
                if (name.IndexOf("jawClose", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("mouthClose", StringComparison.OrdinalIgnoreCase) >= 0)
                    weight = 100f;
                else if (name.IndexOf("jawOpen", StringComparison.OrdinalIgnoreCase) >= 0)
                    weight = 0f;
                else
                    continue;

                list.Add(new ShapeBind
                {
                    Renderer = skins[i],
                    Index = s,
                    Weight = weight,
                });
            }
        }

        _shapes = list.ToArray();
    }

    void ApplyCached()
    {
        for (int i = 0; i < _shapes.Length; i++)
        {
            SkinnedMeshRenderer renderer = _shapes[i].Renderer;
            if (renderer != null)
                renderer.SetBlendShapeWeight(_shapes[i].Index, _shapes[i].Weight);
        }
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
