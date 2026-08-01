using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 判定器：一种「原子条件」的实现。以 <see cref="ConditionEvaluatorAttribute"/> 标注即可被自动发现 / 注册。
    /// 上层系统（道具 / 角色 / …）通过实现本接口扩展自己的条件，核心不认识任何领域概念。
    /// </summary>
    public interface IConditionEvaluator
    {
        /// <summary>唯一键（如 <c>Condition.NumberAtLeast</c>）。</summary>
        string Key { get; }

        /// <summary>显示名（编辑器下拉）。</summary>
        string DisplayName { get; }

        /// <summary>分类（编辑器分组；可由 Key 前缀推导）。</summary>
        string Category { get; }

        /// <summary>参数 schema（编辑器据此生成动态参数区并同步）。</summary>
        IReadOnlyList<ConditionParamDef> ParamSchema { get; }

        /// <summary>判定：读取参数与上下文，返回是否成立。</summary>
        bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx);
    }
}
