using System.Collections.Generic;
using Ale.Toolkit.Editor;

namespace Ale.Condition.Editor
{
    /// <summary>toolkit 主列表面板闭合到 <see cref="ConditionDatabase"/>：Inspector 以 <see cref="IConditionEditorContext"/> 为参。</summary>
    public abstract class ConditionMasterListPanel<T>
        : EditorMasterListPanel<ConditionDatabase, T> where T : class
    {
        public sealed override void DrawInspector(IEditorDbContext<ConditionDatabase> ctx, T item)
            => DrawInspector((IConditionEditorContext)ctx, item);

        public abstract void DrawInspector(IConditionEditorContext ctx, T item);

        protected sealed override void BeforeDrawList(IEditorDbContext<ConditionDatabase> ctx)
            => BeforeDrawList((IConditionEditorContext)ctx);

        protected virtual void BeforeDrawList(IConditionEditorContext ctx) { }
    }

    /// <summary>toolkit 实体列表面板闭合到 <see cref="ConditionDatabase"/>：查重种类 + 以 <see cref="IConditionEditorContext"/> 为参的新增回调。</summary>
    public abstract class ConditionEntityListPanel<TEntity, TTemplate>
        : EditorEntityListPanel<ConditionDatabase, TEntity, TTemplate>
        where TEntity : class where TTemplate : class
    {
        protected ConditionEntityListPanel(string dragId) : base(dragId) { }

        protected abstract EConditionEntityKind Kind { get; }

        protected sealed override HashSet<string> DuplicateIds(IEditorDbContext<ConditionDatabase> ctx)
            => ((IConditionEditorContext)ctx).DuplicateIdsOf(Kind);

        protected sealed override TEntity AddFromTemplate(IEditorDbContext<ConditionDatabase> ctx, string templateName)
            => AddFromTemplate((IConditionEditorContext)ctx, templateName);

        protected abstract TEntity AddFromTemplate(IConditionEditorContext ctx, string templateName);

        protected sealed override TEntity QuickAdd(IEditorDbContext<ConditionDatabase> ctx)
            => QuickAdd((IConditionEditorContext)ctx);

        protected abstract TEntity QuickAdd(IConditionEditorContext ctx);
    }

    /// <summary>toolkit 三列页签闭合到 <see cref="ConditionDatabase"/>。</summary>
    public abstract class ConditionThreeColumnTab<TEntity>
        : EditorThreeColumnTab<ConditionDatabase, TEntity> where TEntity : class
    {
        protected sealed override TEntity DrawEntityList(IEditorDbContext<ConditionDatabase> ctx, TEntity displaySelected)
            => DrawEntityList((IConditionEditorContext)ctx, displaySelected);

        protected abstract TEntity DrawEntityList(IConditionEditorContext ctx, TEntity displaySelected);

        protected sealed override void DrawEntityInspector(IEditorDbContext<ConditionDatabase> ctx, TEntity entity)
            => DrawEntityInspector((IConditionEditorContext)ctx, entity);

        protected abstract void DrawEntityInspector(IConditionEditorContext ctx, TEntity entity);
    }
}
