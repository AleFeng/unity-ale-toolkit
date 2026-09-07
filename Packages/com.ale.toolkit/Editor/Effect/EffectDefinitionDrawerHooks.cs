using System;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 宿主注入点：效果定义绘制器里「属性 id」字段的绘制方式。默认为普通文本框；有属性目录的宿主（如角色系统的核心属性）
    /// 在编辑器初始化时注入下拉：<c>EffectDefinitionDrawerHooks.AttributeIdField = (rect, cur) =&gt; MyPopup(rect, cur);</c>。
    /// 回调签名：<c>(Rect rect, string current) =&gt; string newValue</c>。
    /// </summary>
    public static class EffectDefinitionDrawerHooks
    {
        /// <summary>属性 id 字段绘制回调；null = 文本框。</summary>
        public static Func<Rect, string, string> AttributeIdField;

        /// <summary>
        /// 是否绘制「基本」节（id / 显示名两行）。宿主把定义包在自带 id / 名称的实体里、由实体同步这两个字段时，
        /// 在绘制前置 false、绘制后还原，避免出现两处可编辑却被同步覆盖的字段。默认 true。
        /// </summary>
        public static bool ShowIdentityFields = true;

        /// <summary>按当前钩子绘制属性 id 字段。</summary>
        public static string DrawAttributeId(Rect rect, string current)
        {
            var hook = AttributeIdField;
            return hook != null ? hook(rect, current) : EditorGUI.TextField(rect, current ?? string.Empty);
        }
    }
}
