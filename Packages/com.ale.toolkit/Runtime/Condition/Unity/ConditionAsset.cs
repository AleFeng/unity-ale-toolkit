using UnityEngine;

namespace Ale.Condition
{
    /// <summary>
    /// 可选的条件表达式 SO 容器：满足「把一段条件持久化为独立资产」的需求，也是内联绘制器的一个落点
    /// （新建后在 Inspector 里即可用两级 AND/OR 编辑器配置）。
    /// </summary>
    /// <remarks>
    /// 层级约定：此 SO 字段遵循仓库常规「private + <c>[SerializeField]</c>」；
    /// 仅 <see cref="ConditionExpression"/> 内部的 groups / items 等因引擎无关而用 public 字段。
    /// </remarks>
    [CreateAssetMenu(menuName = "Ale/Condition/Condition Asset", fileName = "ConditionAsset")]
    public class ConditionAsset : ScriptableObject
    {
        [SerializeField] private ConditionExpression expression = new ConditionExpression();

        /// <summary>只读访问表达式。</summary>
        public ConditionExpression Expression => expression;

        /// <summary>用给定上下文求值（转调 <see cref="ConditionEngine.Evaluate"/>）。</summary>
        public ConditionResult Evaluate(IConditionContext ctx, ConditionRegistry registry = null, bool collectAll = false)
            => ConditionEngine.Evaluate(expression, ctx, registry, collectAll);
    }
}
