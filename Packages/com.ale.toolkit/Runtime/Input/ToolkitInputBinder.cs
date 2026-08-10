using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ale.Toolkit.Runtime.InputSupport
{
    // ── 组合键 ────────────────────────────────────────────────────────────────────
    /// <summary>「ActionMap 名 + Action 名」组合键。绑定登记与包装体查表都以它为键。</summary>
    internal readonly struct ActionKey : IEquatable<ActionKey>
    {
        public readonly string Map;
        public readonly string Action;

        public ActionKey(string map, string action)
        {
            Map = map;
            Action = action;
        }

        public bool Equals(ActionKey other)
            => string.Equals(Map, other.Map, StringComparison.Ordinal)
            && string.Equals(Action, other.Action, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ActionKey other && Equals(other);

        public override int GetHashCode()
            => ((Map != null ? Map.GetHashCode() : 0) * 397) ^ (Action != null ? Action.GetHashCode() : 0);

        public override string ToString() => $"{Map}/{Action}";
    }

    // ── 输入绑定门面 ──────────────────────────────────────────────────────────────
    /// <summary>
    /// 按「ActionMap 名 + Action 名」把回调接到 Input System 上的静态门面。解决的是同一件麻烦事：
    /// <b>要绑定的时候，输入源往往还不存在</b>——<c>PlayerInput</c> 常随玩家 / 角色在运行时生成，
    /// 而调用方通常在自己的 <c>OnEnable</c> 里就想把回调挂上去。
    ///
    /// <para>本门面把这段「等它出现再绑」的样板收拢起来：<see cref="Bind(string,string,Action{InputAction.CallbackContext})"/>
    /// 立刻登记，能当场生效就当场生效，不能则挂起、由常驻运行器逐帧重试，直到输入源与对应 Action 可得为止，
    /// 期间不会丢回调。<see cref="Unbind(string,string,Action{InputAction.CallbackContext})"/>
    /// 无论绑定已生效还是仍挂起都能正确撤销。</para>
    ///
    /// <para><b>三个阶段全订阅</b>：回调会挂到 Action 的 <c>started</c> / <c>performed</c> / <c>canceled</c> 上。
    /// 调用方常靠「按下 / 抬起」成对触发来维护拖拽之类的状态（典型写法是在回调里
    /// <c>ctx.ReadValue&lt;float&gt;()</c> 比对 0 / 1），只订 <c>performed</c> 会收不到抬起。
    /// 只关心「触发了一次」的场合请用 <see cref="Bind(string,string,Action)"/> 无参重载。</para>
    ///
    /// <para><b>不改动输入的启用状态</b>：本门面只负责接线，不会 <c>Enable</c> 任何 ActionMap——
    /// 启停由 <c>PlayerInput</c> 的 Default Map / <c>SwitchCurrentActionMap</c> 之类的输入状态机统一决定，
    /// 两件事分开才不会互相打架。绑定到一个当前处于禁用状态的 Map 时会给出一次警告。</para>
    ///
    /// <para>仅在启用 ATK_INPUT_SYSTEM 宏时编译（所属程序集受 defineConstraints 约束）。</para>
    /// </summary>
    public static class ToolkitInputBinder
    {
        #region 输入源

        /// <summary>
        /// 显式指定的输入资源。为空（默认）时回退到「场景中第一个 <see cref="PlayerInput"/> 的 actions」。
        /// <para>改写此值会把已生效的绑定全部退订并重新挂起，按新输入源重新解析——
        /// 因此可在切换输入资源后无需调用方重新 Bind。</para>
        /// </summary>
        public static InputActionAsset Actions
        {
            get
            {
                var runner = ToolkitInputRunner.Instance;
                return runner ? runner.ExplicitActions : null;
            }
            set
            {
                var runner = EnsureRunner();
                if (runner) runner.SetExplicitActions(value);
            }
        }

        #endregion

        #region 绑定 / 解绑

        /// <summary>
        /// 绑定回调到指定的 Action，订阅其 <c>started</c> / <c>performed</c> / <c>canceled</c> 三个阶段。
        /// 输入源尚不可得时挂起、逐帧重试，不会丢失回调。同一 (map, action, 回调) 重复绑定为无操作。
        /// </summary>
        /// <param name="actionMapName">ActionMap 名称。</param>
        /// <param name="actionName">Action 名称。</param>
        /// <param name="callback">回调。</param>
        public static void Bind(
            string actionMapName, string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (callback == null || !ValidateNames(actionMapName, actionName)) return;

            var runner = EnsureRunner();
            if (runner) runner.Bind(new ActionKey(actionMapName, actionName), callback);
        }

        /// <summary>
        /// 绑定无参回调到指定的 Action，<b>只在 <c>performed</c> 阶段</b>触发。
        /// 适用于只关心「触发了一次」、不需要按下 / 抬起区分的场合。
        /// </summary>
        /// <param name="actionMapName">ActionMap 名称。</param>
        /// <param name="actionName">Action 名称。</param>
        /// <param name="onPerformed">回调。</param>
        public static void Bind(string actionMapName, string actionName, Action onPerformed)
        {
            if (onPerformed == null || !ValidateNames(actionMapName, actionName)) return;

            var runner = EnsureRunner();
            if (runner) runner.BindPerformed(new ActionKey(actionMapName, actionName), onPerformed);
        }

        /// <summary>
        /// 解绑回调。绑定已生效则退订，仍在挂起则从待生效队列移除；两种情况都返回 <c>true</c>。
        /// </summary>
        /// <returns>是否确实存在这样一条绑定并已撤销。</returns>
        public static bool Unbind(
            string actionMapName, string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (callback == null) return false;

            var runner = ToolkitInputRunner.Instance;
            // 运行器不存在 = 从未绑定过任何东西，无需（也无从）解绑。
            return runner && runner.Unbind(new ActionKey(actionMapName, actionName), callback);
        }

        /// <summary>解绑由 <see cref="Bind(string,string,Action)"/> 绑定的无参回调。</summary>
        /// <returns>是否确实存在这样一条绑定并已撤销。</returns>
        public static bool Unbind(string actionMapName, string actionName, Action onPerformed)
        {
            if (onPerformed == null) return false;

            var runner = ToolkitInputRunner.Instance;
            return runner && runner.UnbindPerformed(new ActionKey(actionMapName, actionName), onPerformed);
        }

        /// <summary>退订并清空全部绑定登记（含仍在挂起的）。用于整体重置输入接线。</summary>
        public static void UnbindAll()
        {
            var runner = ToolkitInputRunner.Instance;
            if (runner) runner.UnbindAll();
        }

        #endregion

        #region 内部

        private static bool ValidateNames(string actionMapName, string actionName)
        {
            if (!string.IsNullOrEmpty(actionMapName) && !string.IsNullOrEmpty(actionName)) return true;

            Debug.LogWarning("[Toolkit.Input] ActionMap 名或 Action 名为空，该次绑定 / 解绑已忽略。");
            return false;
        }

        // 是否已就「编辑模式下调用输入绑定」告警过，避免刷屏。
        private static bool _warnedEditMode;

        // 惰性获取 / 创建常驻运行器。ToolkitMonoSingleton.Instance 不自建，故此处显式创建。
        private static ToolkitInputRunner EnsureRunner()
        {
            if (ToolkitInputRunner.IsQuitting) return null;

            var inst = ToolkitInputRunner.Instance;
            if (inst) return inst;

            // 编辑模式下不自建运行器。输入系统此时并不驱动玩家循环，绑定不会有任何效果；
            // 而运行器没有 [ExecuteAlways]，其 Awake 不会被调用 —— 单例登记不发生，
            // 于是每次调用都会新造一个 "[ToolkitInput]" 对象，落进用户正在编辑的场景里。
            // 与其静默泄漏，不如明确拒绝并说明原因。
            if (!Application.isPlaying)
            {
                if (!_warnedEditMode)
                {
                    _warnedEditMode = true;
                    Debug.LogWarning(
                        "ToolkitInputBinder >> 在编辑模式下调用了输入绑定，已忽略。" +
                        "输入绑定只在播放模式下有意义，请把调用移到运行时（例如 Start / OnEnable）。");
                }
                return null;
            }

            var go = new GameObject("[ToolkitInput]");
            return go.AddComponent<ToolkitInputRunner>(); // Awake: 设 Instance + DontDestroyOnLoad
        }

        #endregion
    }
}
