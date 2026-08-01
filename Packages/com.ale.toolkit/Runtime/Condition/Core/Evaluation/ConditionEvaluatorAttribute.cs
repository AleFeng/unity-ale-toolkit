using System;

namespace Ale.Condition
{
    /// <summary>
    /// 标注判定器类并声明其键。编辑期（TypeCache）与运行期
    /// （反射，见 <see cref="ConditionRegistry.AutoRegisterFromAssemblies"/>）共用此标记来发现判定器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ConditionEvaluatorAttribute : Attribute
    {
        /// <summary>判定器键（应与实现的 <see cref="IConditionEvaluator.Key"/> 一致）。</summary>
        public string Key { get; }

        public ConditionEvaluatorAttribute(string key) { Key = key; }
    }
}
