namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 可池化对象的生命周期回调接口。挂在被 <see cref="ToolkitGameObjectPool"/> 池化的预制体的
    /// 组件上；当池的 <see cref="ToolkitGameObjectPool.Notification"/> 设为
    /// <see cref="ToolkitGameObjectPool.PoolNotificationType.IPoolable"/>（仅根对象）或
    /// <see cref="ToolkitGameObjectPool.PoolNotificationType.BroadcastIPoolable"/>（含子级）时，
    /// 池会在取用 / 归还时分别回调 <see cref="OnSpawn"/> / <see cref="OnDespawn"/>。
    ///
    /// <para><b>与 <c>OnEnable</c>/<c>OnDisable</c> 的区别：</b>本接口<b>只在池化取还时</b>触发，
    /// 适合放「绑定 / 解绑数据、复位视觉状态」这类逻辑；而 <c>OnEnable</c>/<c>OnDisable</c>
    /// 还会被池外的激活切换触发。每次 <c>Spawn</c>（无论新建还是复用旧实例）都会调用一次
    /// <see cref="OnSpawn"/>，每次 <c>Despawn</c> 都会调用一次 <see cref="OnDespawn"/>。</para>
    /// </summary>
    public interface IPoolable
    {
        /// <summary>实例被取用（Spawn）时调用。此刻对象已激活、已就位。</summary>
        void OnSpawn();

        /// <summary>实例被归还（Despawn）时调用。约定在此清空运行时数据、复位视觉状态；调用后对象随即被停用。</summary>
        void OnDespawn();
    }
}
