using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>效果系统内联绘制器的少量共享样式 / 颜色（暖色调，与条件系统的冷色区分）。</summary>
    internal static class EffectSystemStyles
    {
        /// <summary>阶段组行背景色（暖色轻底）。</summary>
        public static readonly Color GroupBg = new Color(0.55f, 0.42f, 0.28f, 0.16f);

        /// <summary>效果项行背景色。</summary>
        public static readonly Color ItemBg = new Color(0.30f, 0.30f, 0.30f, 0.12f);

        private static GUIStyle _mini;
        /// <summary>紧凑小按钮。</summary>
        public static GUIStyle Mini => _mini ?? (_mini = new GUIStyle(EditorStyles.miniButton)
        {
            padding = new RectOffset(4, 4, 1, 1),
        });
    }
}
