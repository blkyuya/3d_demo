// AudioCatalogSO 和 AudioManager 使用的逻辑键，与具体音频文件名解耦。
// 在 ScriptableObject 里填相同字符串绑定对应 AudioClip；改文件名时只需改 SO，不用改代码。
public static class AudioKeys
{
    public const string PistolShot       = "pistol_shot";
    public const string ShotgunBlast     = "shotgun_blast";
    public const string GunReload        = "gun_reload";
    public const string GunEmpty         = "gun_empty";
    public const string ItemPickup       = "item_pickup";
    public const string ButtonClick      = "button_click";
    public const string DoorCreak        = "door_creak";
    public const string HealSound        = "heal_sound";
    public const string ZombieGroan      = "zombie_groan";
    public const string ZombieMoan       = "zombie_moan";
    public const string Footstep         = "footstep";
    public const string BgmDarkAmbience  = "bgm_dark_ambience";
    public const string BgmHorrorAmbience = "bgm_horror_ambience";
}
