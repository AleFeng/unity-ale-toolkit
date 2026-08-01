using System;

namespace Ale.Effect
{
    /// <summary>
    /// 标注执行器类并声明其键。编辑期（TypeCache）与运行期
    /// （反射，见 <see cref="EffectRegistry.AutoRegisterFromAssemblies"/>）共用此标记来发现执行器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class EffectExecutorAttribute : Attribute
    {
        /// <summary>执行器键（应与实现的 <see cref="IEffectExecutor.Key"/> 一致）。</summary>
        public string Key { get; }

        public EffectExecutorAttribute(string key) { Key = key; }
    }
}
