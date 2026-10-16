namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 可复用对象生命周期回调，由对象池在取出与归还时触发。
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();

        void OnDespawn();
    }
}
