// 玩家玩法层状态枚举，与 Animator Controller 的状态名可对应，但职责分离，互不依赖。
public enum PlayerState
{
    // 默认探索：自由移动，可进入瞄准状态
    Explore,

    // 右键战术瞄准：移动减速，镜头切肩射，准星显示
    Aim,

    // 换弹中：与 HitscanShooter.IsReloading 同步
    Reload,

    // 与场景物体交互；首版用于 Tab 打开背包时的菜单态
    Interact,

    // 使用医疗品等道具的硬直（预留，当前解析器可能落在 Explore）
    Heal,

    // 已死亡：输入和移动由各组件自行截断
    Dead
}
