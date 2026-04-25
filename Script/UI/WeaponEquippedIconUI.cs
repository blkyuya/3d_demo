using UnityEngine;
using UnityEngine.UI;

// 武器图标 HUD：在弹药文字左侧实时显示当前装备的武器图标（手枪/霰弹枪等）。
// Awake 里运行时创建图标 GameObject，自动对齐弹药文字位置，不需要预先在 Hierarchy 里摆好。
// 挂载：Canvas 根节点，Inspector 拖入 pistolSprite / shotgunSprite 图片资源。
public class WeaponEquippedIconUI : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("一般拖 PlayerArmature 上的 WeaponHolder")]
    public WeaponHolder weaponHolder;

    [Header("图标资源")]
    public Sprite pistolSprite;
    public Sprite shotgunSprite;

    [Header("可选")]
    [Tooltip("留空则递归查找名为 AmmoText 的物体")]
    public RectTransform ammoTextRect;

    [Tooltip("相对弹药文字向左偏移（像素）")]
    public float offsetLeftFromAmmo = 130f;

    [Tooltip("图标边长（像素）")]
    public float iconSize = 72f;

    Image _icon;

    // 运行时创建图标 Image，自动和 AmmoText 对齐
    void Awake()
    {
        if (ammoTextRect == null)
        {
            var found = FindDeepChild(transform, "AmmoText");
            if (found != null)
                ammoTextRect = found as RectTransform;
        }

        if (weaponHolder == null)
            weaponHolder = FindObjectOfType<WeaponHolder>();

        Transform parentRoot = ammoTextRect != null ? ammoTextRect.parent : transform;
        Transform existing = parentRoot.Find("WeaponEquippedIconRuntime");
        if (existing != null)
        {
            _icon = existing.GetComponent<Image>();
            return;
        }

        // 动态创建图标节点，避免在 Prefab 里手动摆
        var go = new GameObject("WeaponEquippedIconRuntime", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parentRoot, false);
        _icon = go.GetComponent<Image>();
        var rt = _icon.rectTransform;
        rt.sizeDelta = new Vector2(iconSize, iconSize);

        if (ammoTextRect != null)
        {
            rt.anchorMin = ammoTextRect.anchorMin;
            rt.anchorMax = ammoTextRect.anchorMax;
            rt.pivot = ammoTextRect.pivot;
            rt.anchoredPosition = ammoTextRect.anchoredPosition + new Vector2(-offsetLeftFromAmmo, 0f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-200f, 80f);
        }
    }

    // 递归在子物体中按名字查找节点
    static Transform FindDeepChild(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeepChild(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    // 订阅武器切换事件
    void OnEnable()
    {
        if (weaponHolder != null)
            weaponHolder.OnActiveWeaponChanged += RefreshIcon;
    }

    // 取消订阅
    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.OnActiveWeaponChanged -= RefreshIcon;
    }

    // Start 时同步一次初始图标
    void Start()
    {
        RefreshIcon();
    }

    // 根据当前武器下标选对应图标
    void RefreshIcon()
    {
        if (_icon == null || weaponHolder == null) return;

        int i = weaponHolder.CurrentWeaponIndex;
        Sprite s = i <= 0 ? pistolSprite : (shotgunSprite != null ? shotgunSprite : pistolSprite);
        _icon.sprite = s;
        _icon.enabled = s != null;
        if (s != null)
            _icon.preserveAspect = true;
    }
}
