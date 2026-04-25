using UnityEngine;

// 对象池中的临时音效载体，与 AudioManager 成对使用。
// 每次从池取出时由 AudioManager 配置 AudioSource 参数再 Play；播完后由协程回收。
[RequireComponent(typeof(AudioSource))]
public sealed class PooledOneShotAudio : MonoBehaviour
{
    AudioSource _source;

    // 缓存 AudioSource，关闭自动播放和循环，由 AudioManager 手动控制
    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
    }

    // AudioManager 通过这个属性配置播放参数
    public AudioSource Source => _source;
}
