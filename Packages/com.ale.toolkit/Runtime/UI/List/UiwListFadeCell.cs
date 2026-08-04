using System;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// 列表单元格「根 CanvasGroup 淡入淡出」通用基类，实现 <see cref="IUiwRecycleFadeCell"/>：
    /// 被 <see cref="UiwVirtualListBase{TData,TCell}"/> 的默认 hook 驱动——分配（滚入）时整格淡入、
    /// 回收（滚出）时整格淡出。淡入淡出经 <see cref="ToolkitTween.FadeCanvasGroup"/>；根 CanvasGroup
    /// 惰性获取 / 补挂（预制体未挂则运行时添加，默认表现不变）。
    ///
    /// <para>继承 <see cref="UiwHoverTooltipSource"/>，故列表单元格可同时具备悬停弹窗与淡入淡出能力。
    /// 具体业务单元格（道具 / 技能等）继承本类即白得通用淡入淡出，无需各自实现。</para>
    /// </summary>
    public abstract class UiwListFadeCell : UiwHoverTooltipSource, IUiwRecycleFadeCell
    {
        [Header("淡入淡出（Tween）")]
        [Tooltip("格子被分配（滚入）时，根 CanvasGroup 的淡入时长（秒）。")]
        public float rootFadeInDuration  = 0.15f;
        [Tooltip("格子被回收（滚出）时，根 CanvasGroup 的淡出时长（秒）。")]
        public float rootFadeOutDuration = 0.12f;

        private ToolkitTweenHandle _rootFade;

        // 惰性根 CanvasGroup：预制体未挂则运行时补挂（默认 alpha=1 / 可交互 / 挡射线，不改变原有表现）。
        private CanvasGroup _rootCanvasGroup;
        /// <summary>本格根 CanvasGroup（惰性获取 / 补挂）。</summary>
        protected CanvasGroup RootCanvasGroup
        {
            get
            {
                if (_rootCanvasGroup) return _rootCanvasGroup;
                if (!TryGetComponent(out _rootCanvasGroup))
                    _rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                return _rootCanvasGroup;
            }
        }

        /// <summary>
        /// 是否启用本格的通用回收淡入淡出（安全阀）。默认 true；子类可 override 为 false 退出
        /// （如根 CanvasGroup 另作它用、与淡入淡出冲突时）。关闭时淡入为空操作、淡出立即完成（引擎即时回收）。
        /// </summary>
        protected virtual bool RecycleFadeEnabled => true;

        /// <summary>分配（首次显示）时淡入（alpha 0 → 1）。由列表引擎在格子生成绑定后调用。</summary>
        public void PlayShowFade()
        {
            if (!RecycleFadeEnabled) return;
            var cg = RootCanvasGroup;
            if (!cg) return;
            _rootFade.Kill();
            cg.blocksRaycasts = true;   // 恢复可点（淡出时被置 false）
            cg.alpha = 0f;
            _rootFade = ToolkitTween.FadeCanvasGroup(cg, 1f, rootFadeInDuration);
        }

        /// <summary>
        /// 回收时淡出（alpha → 0），完成后回调 <paramref name="onComplete"/>（引擎在其中清空 / 归还本格）。
        /// 淡出期间置 blocksRaycasts=false，避免对陈旧内容触发点击。安全阀关闭或无 CanvasGroup 时立即回调。
        /// </summary>
        public void FadeOutAndHide(Action onComplete)
        {
            var cg = RecycleFadeEnabled ? RootCanvasGroup : null;
            if (!cg) { onComplete?.Invoke(); return; }
            _rootFade.Kill();
            cg.blocksRaycasts = false;
            _rootFade = ToolkitTween.FadeCanvasGroup(cg, 0f, rootFadeOutDuration, onComplete: onComplete);
        }

        /// <summary>打断在途的根淡入 / 淡出（不触发其完成回调）。供列表回收动画取消。</summary>
        public void CancelRootFade() => _rootFade.Kill();

        /// <summary>
        /// 复位根显示：打断在途根淡变并把根 CanvasGroup 复位为完全显示（alpha 1、可点）。
        /// 仅当已存在根 CanvasGroup 时才处理（未淡过的格子无 CanvasGroup、本就完全显示）。
        /// 供「淡出归还的实例被复用为可见空格」等场景复位，避免根 alpha 停在 0 而隐身。
        /// </summary>
        protected void ResetRootVisible()
        {
            CancelRootFade();
            if (_rootCanvasGroup) { _rootCanvasGroup.alpha = 1f; _rootCanvasGroup.blocksRaycasts = true; }
        }
    }
}
