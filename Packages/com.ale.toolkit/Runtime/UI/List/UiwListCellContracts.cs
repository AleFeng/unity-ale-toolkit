using System;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// 虚拟列表单元格「回收淡入淡出」契约。单元格实现本接口后，
    /// <see cref="UiwVirtualListBase{TData,TCell}"/> 的默认回收 / 生成 hook 会自动驱动其淡入淡出
    /// —— 各列表无需再 override。Toolkit 提供的通用实现为 <c>UiwListFadeCell</c>。
    /// </summary>
    public interface IUiwRecycleFadeCell
    {
        /// <summary>分配（首次显示）时淡入。由列表引擎在格子生成绑定后调用。</summary>
        void PlayShowFade();

        /// <summary>
        /// 回收时淡出，淡出结束回调 <paramref name="onComplete"/>（引擎在其中清空 / 归还本格）。
        /// 本方法只负责淡出动画；真正的清空 / 隐藏由引擎在回调里完成，避免与格子清空逻辑重复执行。
        /// </summary>
        void FadeOutAndHide(Action onComplete);

        /// <summary>打断在途的淡入 / 淡出（不触发其完成回调）。</summary>
        void CancelRootFade();
    }

    /// <summary>
    /// 虚拟列表单元格「增量差异刷新」契约：判断本格当前显示是否与给定数据一致。
    /// 单元格实现本接口后，<see cref="UiwVirtualListBase{TData,TCell}"/> 的默认 <c>NeedsRebind</c>
    /// 会据此跳过未变格的重绑（避免图标异步重载闪烁与无谓开销）；未实现则默认全部重绑。
    /// </summary>
    /// <typeparam name="TData">列表数据元素类型（逆变，仅作输入）。</typeparam>
    public interface IUiwDiffCell<in TData>
    {
        /// <summary>本格当前显示内容是否与 <paramref name="data"/> 一致（true = 一致，可跳过重绑）。</summary>
        bool MatchesSlot(TData data);
    }
}
