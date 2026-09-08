using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 具名条件注册表：聚合多个 <see cref="IConditionDefinitionSource"/>（各宿主条件库）与直接登记的表达式，按 id 查找、先登记先得。
    /// 解决「技能树的解锁条件引用了角色系统条件库里定义的那条条件」这类<b>跨库引用</b>——求值时若上下文没有条件源，回落到 <see cref="Default"/>。
    /// Unity 桥在每次播放开始清空它（关闭 Domain Reload 时不串数据）。与效果侧的 <c>EffectDefinitionRegistry</c> 对偶。
    /// </summary>
    public sealed class ConditionDefinitionRegistry : IConditionDefinitionSource
    {
        /// <summary>共享默认实例。</summary>
        public static ConditionDefinitionRegistry Default { get; } = new ConditionDefinitionRegistry();

        private readonly Dictionary<string, ConditionExpression> _local = new Dictionary<string, ConditionExpression>();
        private readonly List<IConditionDefinitionSource> _sources = new List<IConditionDefinitionSource>();

        /// <summary>已聚合的来源数。</summary>
        public int SourceCount => _sources.Count;

        /// <summary>直接登记的条件数。</summary>
        public int LocalCount => _local.Count;

        /// <summary>加入一个来源（去重；忽略 null 与自身）。</summary>
        public void AddSource(IConditionDefinitionSource source)
        {
            if (source == null || ReferenceEquals(source, this) || _sources.Contains(source)) return;
            _sources.Add(source);
        }

        /// <summary>移除一个来源。</summary>
        public bool RemoveSource(IConditionDefinitionSource source) => source != null && _sources.Remove(source);

        /// <summary>直接登记一条具名条件（同 id 覆盖；id 或表达式为空则忽略）。</summary>
        public void Register(string id, ConditionExpression expression)
        {
            if (string.IsNullOrEmpty(id) || expression == null) return;
            _local[id] = expression;
        }

        /// <summary>注销直接登记的条件。</summary>
        public bool Unregister(string id) => id != null && _local.Remove(id);

        /// <summary>按 id 查找：直接登记优先，再按来源加入顺序逐个询问；未找到返回 null。</summary>
        public ConditionExpression GetCondition(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_local.TryGetValue(id, out var e)) return e;
            for (int i = 0; i < _sources.Count; i++)
            {
                var found = _sources[i]?.GetCondition(id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>按 id 查找。</summary>
        public bool TryGet(string id, out ConditionExpression expression)
        {
            expression = GetCondition(id);
            return expression != null;
        }

        /// <summary>清空来源与直接登记。</summary>
        public void Clear()
        {
            _local.Clear();
            _sources.Clear();
        }
    }
}
