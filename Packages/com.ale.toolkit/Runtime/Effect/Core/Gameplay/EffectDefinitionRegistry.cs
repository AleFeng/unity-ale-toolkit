using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 效果定义注册表：聚合多个 <see cref="IEffectDefinitionSource"/>（各宿主数据库）与直接登记的定义，按 id 查找、先登记先得。
    /// 解决「道具引用了角色系统数据库里定义的效果」这类<b>跨库引用</b>——施加时若上下文没有定义源，回落到 <see cref="Default"/>。
    /// Unity 桥在每次播放开始清空它（关闭 Domain Reload 时不串数据）。
    /// </summary>
    public sealed class EffectDefinitionRegistry : IEffectDefinitionSource
    {
        /// <summary>共享默认实例。</summary>
        public static EffectDefinitionRegistry Default { get; } = new EffectDefinitionRegistry();

        private readonly Dictionary<string, EffectDefinition> _local = new Dictionary<string, EffectDefinition>();
        private readonly List<IEffectDefinitionSource> _sources = new List<IEffectDefinitionSource>();

        /// <summary>已聚合的来源数。</summary>
        public int SourceCount => _sources.Count;

        /// <summary>直接登记的定义数。</summary>
        public int LocalCount => _local.Count;

        /// <summary>加入一个来源（去重；忽略 null 与自身）。</summary>
        public void AddSource(IEffectDefinitionSource source)
        {
            if (source == null || ReferenceEquals(source, this) || _sources.Contains(source)) return;
            _sources.Add(source);
        }

        /// <summary>移除一个来源。</summary>
        public bool RemoveSource(IEffectDefinitionSource source) => source != null && _sources.Remove(source);

        /// <summary>直接登记一条定义（按 id；同 id 覆盖；id 空则忽略）。</summary>
        public void Register(EffectDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return;
            _local[definition.id] = definition;
        }

        /// <summary>注销直接登记的定义。</summary>
        public bool Unregister(string id) => id != null && _local.Remove(id);

        /// <summary>按 id 查找：直接登记优先，再按来源加入顺序逐个询问；未找到返回 null。</summary>
        public EffectDefinition GetEffect(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_local.TryGetValue(id, out var d)) return d;
            for (int i = 0; i < _sources.Count; i++)
            {
                var found = _sources[i]?.GetEffect(id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>按 id 查找。</summary>
        public bool TryGet(string id, out EffectDefinition definition)
        {
            definition = GetEffect(id);
            return definition != null;
        }

        /// <summary>清空来源与直接登记。</summary>
        public void Clear()
        {
            _local.Clear();
            _sources.Clear();
        }
    }
}
