using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>条件系统内联绘制器的少量共享样式 / 颜色。</summary>
    internal static class ConditionSystemStyles
    {
        /// <summary>条件组行背景色（暗色轻底）。</summary>
        public static readonly Color GroupBg = new Color(0.30f, 0.42f, 0.55f, 0.16f);

        /// <summary>条件项行背景色。</summary>
        public static readonly Color ItemBg = new Color(0.30f, 0.30f, 0.30f, 0.12f);

        private static GUIStyle _mini;
        /// <summary>紧凑小按钮。</summary>
        public static GUIStyle Mini => _mini ?? (_mini = new GUIStyle(EditorStyles.miniButton)
        {
            padding = new RectOffset(4, 4, 1, 1),
        });
    }
}
