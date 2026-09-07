using UnityEditor;
using UnityEngine;

namespace Ale.GameplayTags.Editor
{
    /// <summary>标签系统绘制器的少量共享样式 / 颜色（绿色系，与条件的冷色、效果的暖色区分）。</summary>
    internal static class GameplayTagSystemStyles
    {
        /// <summary>容器控制行背景色。</summary>
        public static readonly Color GroupBg = new Color(0.30f, 0.55f, 0.38f, 0.16f);

        /// <summary>非法标签名：红色覆层（画在字段之上，半透明）。</summary>
        public static readonly Color InvalidBg = new Color(0.90f, 0.25f, 0.25f, 0.28f);

        /// <summary>合法但目录未登记：黄色覆层。</summary>
        public static readonly Color UnknownBg = new Color(0.90f, 0.72f, 0.20f, 0.22f);

        private static GUIStyle _mini;
        /// <summary>紧凑小按钮。</summary>
        public static GUIStyle Mini => _mini ?? (_mini = new GUIStyle(EditorStyles.miniButton)
        {
            padding = new RectOffset(4, 4, 1, 1),
        });
    }
}
