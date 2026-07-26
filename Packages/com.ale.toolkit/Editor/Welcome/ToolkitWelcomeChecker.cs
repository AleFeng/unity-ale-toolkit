using UnityEditor;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// Ale Toolkit 欢迎窗口自动显示器（<c>[InitializeOnLoad]</c>）。每次 Unity 启动 / 域重载后延迟执行：
    /// 若本会话尚未显示过、且未禁用「启动时自动显示」，则自动弹出 toolkit 欢迎窗口。
    ///
    /// <para>开关 <see cref="ToolkitWelcomeWindow.AutoShowKey"/> 为<b>每人自定</b>的 <see cref="EditorPrefs"/> 偏好
    /// （默认开启，不入版本控制）；本会话是否已显示以 <see cref="SessionState"/> 记录（重启 Unity 后重置）。
    /// 与库存的 <c>InventoryDefineChecker</c> 各自独立，两窗互不影响。</para>
    /// </summary>
    [InitializeOnLoad]
    public static class ToolkitWelcomeChecker
    {
        static ToolkitWelcomeChecker()
        {
            // 延迟到编辑器完全就绪后执行，避免在域初始化期间操作 UI。
            EditorApplication.delayCall += OnDelayedInit;
        }

        private static void OnDelayedInit()
        {
            EditorApplication.delayCall -= OnDelayedInit;

            // 本会话已经显示过则跳过。
            if (SessionState.GetBool(ToolkitWelcomeWindow.ShownThisSessionKey, false))
                return;
            SessionState.SetBool(ToolkitWelcomeWindow.ShownThisSessionKey, true);

            // 用户禁用了自动显示则跳过。
            if (!EditorPrefs.GetBool(ToolkitWelcomeWindow.AutoShowKey, true))
                return;

            ToolkitWelcomeWindow.Open();
        }
    }
}
