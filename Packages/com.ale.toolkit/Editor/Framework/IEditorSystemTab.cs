using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 「系统页签」对实体类型<b>非泛型</b>的契约：让 <see cref="EditorDatabaseWindowBase{TDb}"/> 能把
    /// 不同实体类型的页签（如各种闭合的 <see cref="EditorThreeColumnTab{TDb,TEntity}"/>）以统一数组持有并派发，
    /// 无需知道每个页签具体的 TEntity。
    /// <para><see cref="EditorThreeColumnTab{TDb,TEntity}"/> 已实现本接口；宿主的自定义页签实现本接口即可纳入窗口。</para>
    /// </summary>
    /// <typeparam name="TDb">宿主数据库类型。</typeparam>
    public interface IEditorSystemTab<TDb> where TDb : ScriptableObject
    {
        /// <summary>绘制该页签，<paramref name="rect"/> 为页签可用区域。</summary>
        void OnGUI(Rect rect, IEditorDbContext<TDb> ctx);

        /// <summary>数据库切换后回调（重置选中 / 失效缓存）。</summary>
        void OnDatabaseChanged(IEditorDbContext<TDb> ctx);

        /// <summary>Undo/Redo 后回调（失效缓存）。</summary>
        void OnUndoRedo();
    }
}
