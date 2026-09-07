namespace Ale.Effect
{
    /// <summary>
    /// 便捷施加入口：从上下文解析目标容器（<see cref="IEffectContainerSource"/>，或主体本身就是容器）与效果定义
    /// （请求携带 → 上下文的 <see cref="IEffectDefinitionSource"/> → <see cref="EffectDefinitionRegistry.Default"/>），再交给容器施加。
    /// 道具「使用」、技能「施放」等只知道效果 id 的调用方走这里，不必自己找容器与定义。
    /// </summary>
    public static class EffectApplier
    {
        /// <summary>按请求施加：目标 = <c>request.Target ?? ctx.Subject</c>。找不到容器 / 定义时返回相应的阻断结果。</summary>
        public static EffectApplyResult Apply(EffectApplyRequest request, IEffectContext ctx, EffectDefinitionRegistry fallbackRegistry = null)
        {
            if (request == null) return EffectApplyResult.Blocked(EEffectApplyOutcome.Invalid, "请求为空");

            object target = request.Target ?? ctx?.Subject;
            var container = ResolveContainer(ctx, target);
            if (container == null)
                return EffectApplyResult.Blocked(EEffectApplyOutcome.NoContainer, "上下文无 IEffectContainerSource，或主体没有效果容器");

            var def = ResolveDefinition(request, ctx, fallbackRegistry);
            if (def == null)
                return EffectApplyResult.Blocked(EEffectApplyOutcome.DefinitionNotFound, "找不到效果定义：" + request.EffectId);

            request.Definition = def;
            return container.ApplyEffect(request, ctx);
        }

        /// <summary>按效果 id 施加到上下文主体。</summary>
        public static EffectApplyResult Apply(string effectId, IEffectContext ctx, int level = 1, object source = null, string sourceTag = null)
            => Apply(new EffectApplyRequest(effectId, level) { Source = source, SourceTag = sourceTag }, ctx);

        /// <summary>解析定义：请求携带 → 上下文的 <see cref="IEffectDefinitionSource"/> → <paramref name="fallbackRegistry"/>（默认全局注册表）。</summary>
        public static EffectDefinition ResolveDefinition(EffectApplyRequest request, IEffectContext ctx, EffectDefinitionRegistry fallbackRegistry = null)
        {
            if (request == null) return null;
            if (request.Definition != null) return request.Definition;
            string id = request.EffectId;
            if (string.IsNullOrEmpty(id)) return null;
            var def = ctx?.GetService<IEffectDefinitionSource>()?.GetEffect(id);
            return def ?? (fallbackRegistry ?? EffectDefinitionRegistry.Default).GetEffect(id);
        }

        /// <summary>解析容器：上下文的 <see cref="IEffectContainerSource"/>；否则主体本身是容器时直接用之。</summary>
        public static EffectContainer ResolveContainer(IEffectContext ctx, object subject)
        {
            var container = ctx?.GetService<IEffectContainerSource>()?.GetContainer(subject);
            return container ?? subject as EffectContainer;
        }
    }
}
