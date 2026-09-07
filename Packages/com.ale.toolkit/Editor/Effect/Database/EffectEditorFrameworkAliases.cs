using System.Collections.Generic;
using Ale.Toolkit.Editor;

namespace Ale.Effect.Editor
{
    /// <summary>toolkit 主列表面板闭合到 <see cref="EffectDatabase"/>：Inspector 以 <see cref="IEffectEditorContext"/> 为参。</summary>
    public abstract class EffectMasterListPanel<T>
        : EditorMasterListPanel<EffectDatabase, T> where T : class
    {
        public sealed override void DrawInspector(IEditorDbContext<EffectDatabase> ctx, T item)
            => DrawInspector((IEffectEditorContext)ctx, item);

        public abstract void DrawInspector(IEffectEditorContext ctx, T item);

        protected sealed override void BeforeDrawList(IEditorDbContext<EffectDatabase> ctx)
            => BeforeDrawList((IEffectEditorContext)ctx);

        protected virtual void BeforeDrawList(IEffectEditorContext ctx) { }
    }

    /// <summary>toolkit 实体列表面板闭合到 <see cref="EffectDatabase"/>：查重种类 + 以 <see cref="IEffectEditorContext"/> 为参的新增回调。</summary>
    public abstract class EffectEntityListPanel<TEntity, TTemplate>
        : EditorEntityListPanel<EffectDatabase, TEntity, TTemplate>
        where TEntity : class where TTemplate : class
    {
        protected EffectEntityListPanel(string dragId) : base(dragId) { }

        protected abstract EEffectEntityKind Kind { get; }

        protected sealed override HashSet<string> DuplicateIds(IEditorDbContext<EffectDatabase> ctx)
            => ((IEffectEditorContext)ctx).DuplicateIdsOf(Kind);

        protected sealed override TEntity AddFromTemplate(IEditorDbContext<EffectDatabase> ctx, string templateName)
            => AddFromTemplate((IEffectEditorContext)ctx, templateName);

        protected abstract TEntity AddFromTemplate(IEffectEditorContext ctx, string templateName);

        protected sealed override TEntity QuickAdd(IEditorDbContext<EffectDatabase> ctx)
            => QuickAdd((IEffectEditorContext)ctx);

        protected abstract TEntity QuickAdd(IEffectEditorContext ctx);
    }

    /// <summary>toolkit 三列页签闭合到 <see cref="EffectDatabase"/>。</summary>
    public abstract class EffectThreeColumnTab<TEntity>
        : EditorThreeColumnTab<EffectDatabase, TEntity> where TEntity : class
    {
        protected sealed override TEntity DrawEntityList(IEditorDbContext<EffectDatabase> ctx, TEntity displaySelected)
            => DrawEntityList((IEffectEditorContext)ctx, displaySelected);

        protected abstract TEntity DrawEntityList(IEffectEditorContext ctx, TEntity displaySelected);

        protected sealed override void DrawEntityInspector(IEditorDbContext<EffectDatabase> ctx, TEntity entity)
            => DrawEntityInspector((IEffectEditorContext)ctx, entity);

        protected abstract void DrawEntityInspector(IEffectEditorContext ctx, TEntity entity);
    }
}
