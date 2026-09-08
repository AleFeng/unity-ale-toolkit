using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 编辑器窗口共用的 GUIStyle 与颜色缓存。延迟初始化（首次访问时构建），避免在非 GUI 线程或静态构造期出错。
    /// </summary>
    public static class ToolkitEditorStyles
    {
        private static bool _initialized;

        private static GUIStyle _header;
        private static GUIStyle _redField;
        private static GUIStyle _statusError;
        private static GUIStyle _placeholder;
        private static GUIStyle _colorDot;
        private static GUIStyle _dangerMiniButton;

        /// <summary>重复/错误高亮使用的红色。</summary>
        public static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f);

        /// <summary>列表选中行背景色。</summary>
        public static readonly Color SelectedColor = new Color(0.24f, 0.48f, 0.90f, 0.35f);

        /// <summary>鼠标悬停行的叠加高亮色（浅浅一层，叠在选中 / 重复 ID / 拖拽源等底色之上）。</summary>
        public static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.055f);

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            _header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            _redField = new GUIStyle(EditorStyles.textField);
            _redField.normal.textColor = ErrorColor;
            _redField.focused.textColor = ErrorColor;

            _statusError = new GUIStyle(EditorStyles.label);
            _statusError.normal.textColor = ErrorColor;

            _placeholder = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };

            _colorDot = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(0, 0, 0, 0),
            };

            _dangerMiniButton = new GUIStyle(EditorStyles.miniButton)
            {
                normal = { textColor = new Color(0.9f, 0.45f, 0.45f) },
            };
        }

        public static GUIStyle Header { get { EnsureInit(); return _header; } }
        public static GUIStyle RedField { get { EnsureInit(); return _redField; } }
        public static GUIStyle StatusError { get { EnsureInit(); return _statusError; } }
        public static GUIStyle Placeholder { get { EnsureInit(); return _placeholder; } }

        /// <summary>危险操作的小按钮（红字），如各系统页签的「删除X」。此前六处各自每帧 new 一个 GUIStyle。</summary>
        public static GUIStyle DangerMiniButton { get { EnsureInit(); return _dangerMiniButton; } }

        /// <summary>在指定矩形绘制一层半透明背景色（用于选中/高亮行）。</summary>
        public static void DrawRowBackground(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>
        /// 开启鼠标悬停跟踪：IMGUI 窗口默认不接收 <see cref="EventType.MouseMove"/>，光标移动不会触发重绘，
        /// 悬停高亮就不会跟着走。绘制含可点击行的列表前调用一次即可（每种事件都要走到，别放在 Repaint 分支里）。
        /// </summary>
        /// <param name="ctx">承担重绘的编辑器上下文（窗口 / Inspector）。</param>
        public static void TrackMouseHover(IEditorContext ctx)
        {
            var e = Event.current;
            if (e == null) return;

            // 优先给「正在绘制的这个窗口」打开鼠标移动事件（宿主上下文通常就是 EditorWindow 本身）；
            // 上下文不是窗口时（如 Inspector 里的绘制器）退回鼠标所在窗口。
            var window = ctx as EditorWindow ?? EditorWindow.mouseOverWindow;
            if (window != null && !window.wantsMouseMove) window.wantsMouseMove = true;

            // 移动 → 重画以更新悬停行；移出窗口 → 重画以抹掉残留的高亮。
            if (e.type == EventType.MouseMove || e.type == EventType.MouseLeaveWindow) ctx?.Repaint();
        }

        /// <summary>
        /// 为鼠标所在的行叠加一层悬停高亮，返回本行是否处于悬停。只在 Repaint 阶段生效，
        /// 应在行的各层底色之后、行内容之前调用。需配合 <see cref="TrackMouseHover"/> 才会随光标移动刷新。
        /// </summary>
        public static bool DrawRowHover(Rect rect)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.Repaint) return false;
            if (!rect.Contains(e.mousePosition)) return false;

            EditorGUI.DrawRect(rect, HoverColor);
            return true;
        }

        /// <summary>
        /// 在指定矩形中央绘制一个实心圆点（"●"字符），颜色由 <paramref name="dotColor"/> 指定。
        /// 推荐矩形大小为 14×14。
        /// </summary>
        public static void DrawColorDot(Rect rect, Color dotColor)
        {
            EnsureInit();
            var prev = GUI.color;
            GUI.color = dotColor;
            GUI.Label(rect, "●", _colorDot);
            GUI.color = prev;
        }
    }
}
