using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 执行器：一种「原子效果」的实现（Condition System 判定器 <c>IConditionEvaluator</c> 的写侧对偶）。
    /// 以 <see cref="EffectExecutorAttribute"/> 标注即可被自动发现 / 注册。上层系统（战斗 / 角色 / 技能 / …）
    /// 通过实现本接口扩展自己的效果，核心不认识任何领域概念；具体的写侧突变经上下文的领域服务施加。
    /// </summary>
    public interface IEffectExecutor
    {
        /// <summary>唯一键（如 <c>Combat.Ignite</c>）。</summary>
        string Key { get; }

        /// <summary>显示名（编辑器下拉）。</summary>
        string DisplayName { get; }

        /// <summary>分类（编辑器分组；可由 Key 前缀推导）。</summary>
        string Category { get; }

        /// <summary>参数 schema（编辑器据此生成动态参数区并同步）。</summary>
        IReadOnlyList<EffectParamDef> ParamSchema { get; }

        /// <summary>执行：读取参数与上下文（写侧服务），施加突变，返回轻量结果。</summary>
        EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx);
    }
}
