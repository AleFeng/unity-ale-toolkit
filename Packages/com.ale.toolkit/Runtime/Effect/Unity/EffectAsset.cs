using UnityEngine;
using Ale.Condition;

namespace Ale.Effect
{
    /// <summary>
    /// 可选的效果表达式 SO 容器：把一段效果持久化为独立资产，也是内联绘制器的一个落点
    /// （新建后在 Inspector 里即可用阶段组编辑器配置）。
    /// </summary>
    /// <remarks>
    /// 层级约定：此 SO 字段遵循仓库常规「private + <c>[SerializeField]</c>」；
    /// 仅 <see cref="EffectExpression"/> 内部的 groups / items 等因引擎无关而用 public 字段。
    /// </remarks>
    [CreateAssetMenu(menuName = "Ale/Effect/Effect Asset", fileName = "EffectAsset")]
    public class EffectAsset : ScriptableObject
    {
        [SerializeField] private EffectExpression expression = new EffectExpression();

        /// <summary>只读访问表达式。</summary>
        public EffectExpression Expression => expression;

        /// <summary>用给定上下文执行（转调 <see cref="EffectRunner.Run"/>）。</summary>
        public EffectRunReport Run(IEffectContext ctx, string phase = null,
            EffectRegistry effectRegistry = null, ConditionRegistry conditionRegistry = null)
            => EffectRunner.Run(expression, ctx, phase, effectRegistry, conditionRegistry);
    }
}
