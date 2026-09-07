using System.Collections.Generic;
using Ale.Toolkit.Editor;

namespace Ale.Effect.Editor
{
    /// <summary>效果编辑器中参与「重复 id / name 查重」的实体种类。</summary>
    public enum EEffectEntityKind
    {
        Effect,
        Template,
        EnumType,
    }

    /// <summary>
    /// 效果编辑器面板与主窗口之间的交互契约：通用部分（数据库 / SerializedObject / 解析器 / Undo / 标脏 / 重绘）由 toolkit 的
    /// <see cref="IEditorDbContext{TDb}"/> 闭合到 <see cref="EffectDatabase"/> 提供；本接口在其上补充「按种类查重复 id / name」。
    /// </summary>
    public interface IEffectEditorContext : IEditorDbContext<EffectDatabase>
    {
        /// <summary>取该种类当前重复（或空）的 id / name 集合。</summary>
        HashSet<string> DuplicateIdsOf(EEffectEntityKind kind);
    }
}
