using UnityEngine;

// 武器数据配表（ScriptableObject）：伤害、弹匣、弹药类型、霰弹丸数等，在 Project 里创建实例后拖给 WeaponHolder。
// 策划改数值不用动代码，是数据驱动的体现。
[CreateAssetMenu(menuName = "游戏/武器数据", fileName = "WeaponData")]
public class WeaponDataSO : ScriptableObject
{
    [Header("显示")]
    [Tooltip("UI 与调试显示用名称")]
    public string displayName = "武器";

    [Header("射击")]
    [Min(1), Tooltip("单发伤害（每粒弹丸）")]
    public int damage = 1;

    [Min(1), Tooltip("每次开火射线数量：手枪=1，霰弹>1")]
    public int pelletCount = 1;

    [Tooltip("霰弹散布半角（度）；手枪为 0")]
    [Range(0f, 15f)]
    public float spreadHalfAngleDegrees = 0f;

    [Header("弹药")]
    [Min(1)]
    public int magazineSize = 8;

    public AmmoType ammoType = AmmoType.Pistol9mm;

    [Header("其它")]
    public float shootDistance = 100f;

    [Min(0.05f)]
    public float reloadTime = 1.2f;
}
