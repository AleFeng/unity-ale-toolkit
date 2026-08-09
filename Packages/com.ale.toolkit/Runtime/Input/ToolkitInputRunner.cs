using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ale.Toolkit.Runtime.InputSupport
{
    // 常驻输入绑定运行器：持有全部绑定登记，并按帧重试尚未生效的绑定。跨场景持久、
    // 关闭 Domain Reload 自动复位（均继承自基类）。由 ToolkitInputBinder.EnsureRunner()
    // 惰性创建（GameObject "[ToolkitInput]"），不需要手动挂载。
    //
    // 为什么全部可变状态都放在运行器实例上、而不是门面的静态字段：关闭 Domain Reload 时静态字段
    // 跨播放会话残留，会把上一次播放留下的绑定（其 InputAction 已随输入系统重新初始化而失效）
    // 带进下一次播放；而运行器随播放结束一并销毁，状态天然干净。
    // ToolkitTween / ToolkitTweenRunner 是同一套分工。
    internal sealed class ToolkitInputRunner : ToolkitMonoSingleton<ToolkitInputRunner>
    {
        // 一条绑定登记。
        private sealed class Binding
        {
            public ActionKey Key;
            public Action<InputAction.CallbackContext> Callback;

            // 非空 = 已订阅到该 InputAction；空 = 仍待生效（输入源或对应 Action 尚不可得）。
            // 记住当初订阅的那个实例，而不是解绑时重新查找——这样即便中途换过输入源，
            // 退订的也一定是当初挂上去的那一个，不会留下悬挂的委托。
            public InputAction Applied;
        }

        private readonly List<Binding> _bindings = new List<Binding>();

        // 无参回调 → 其包装体（只在 performed 阶段触发）。登记于此，以便按原始无参回调解绑。
        // 键含 ActionKey：同一个无参回调允许绑到不同的 Action 上。
        private readonly Dictionary<(ActionKey, Action), Action<InputAction.CallbackContext>> _performedWrappers
            = new Dictionary<(ActionKey, Action), Action<InputAction.CallbackContext>>();

        // 已因「ActionMap 未启用」告警过的 Map 名，避免逐帧刷屏。
        private readonly HashSet<string> _warnedDisabledMaps = new HashSet<string>();

        // 待生效的绑定数量。为 0 时 LateUpdate 立即返回，空转开销极低。
        private int _pendingCount;

        private InputActionAsset _explicitActions;
        private InputActionAsset _resolvedActions;

        #region 输入源

        /// <summary>显式指定的输入资源（未指定则为空，回退到场景中第一个 PlayerInput）。</summary>
        public InputActionAsset ExplicitActions => _explicitActions;

        /// <summary>更换显式输入源：把已生效的绑定全部退订并重新挂起，交由重试循环按新源重新解析。</summary>
        public void SetExplicitActions(InputActionAsset actions)
        {
            if (_explicitActions == actions) return;

            _explicitActions = actions;
            _resolvedActions = null;

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding.Applied == null) continue;
                Unsubscribe(binding);
                _pendingCount++;
            }
        }

        /// <summary>
        /// 解析当前可用的输入资源：优先显式指定，否则取场景中第一个 <see cref="PlayerInput"/> 的 actions。
        /// <para>解析失败不算错误——<c>PlayerInput</c> 常随角色在运行时生成，绑定会保持挂起、逐帧重试到它出现。</para>
        /// </summary>
        private InputActionAsset ResolveActions()
        {
            if (_explicitActions) return _explicitActions;
            // 已解析过就复用，避免在「已绑定完毕」之后仍每帧做一次全场景查找。
            if (_resolvedActions) return _resolvedActions;

#if UNITY_2023_1_OR_NEWER
            var playerInput = FindFirstObjectByType<PlayerInput>();
#else
            var playerInput = FindObjectOfType<PlayerInput>();
#endif
            _resolvedActions = playerInput ? playerInput.actions : null;
            return _resolvedActions;
        }

        #endregion

        #region 绑定 / 解绑

        public void Bind(ActionKey key, Action<InputAction.CallbackContext> callback)
        {
            // 去重：同一 (map, action, 回调) 重复绑定视为无操作。
            for (int i = 0; i < _bindings.Count; i++)
                if (_bindings[i].Key.Equals(key) && _bindings[i].Callback == callback)
                    return;

            var binding = new Binding { Key = key, Callback = callback };
            _bindings.Add(binding);

            // 能当场生效就当场生效（少等一帧）；否则计入待生效，由 LateUpdate 逐帧重试。
            if (!TrySubscribe(binding)) _pendingCount++;
        }

        public void BindPerformed(ActionKey key, Action onPerformed)
        {
            var wrapperKey = (key, onPerformed);
            if (_performedWrappers.ContainsKey(wrapperKey)) return; // 已绑定过

            // 包装成三阶段回调后只放行 performed，使无参回调「触发一次就调一次」。
            Action<InputAction.CallbackContext> wrapper = ctx =>
            {
                if (ctx.performed) onPerformed();
            };

            _performedWrappers[wrapperKey] = wrapper;
            Bind(key, wrapper);
        }

        public bool Unbind(ActionKey key, Action<InputAction.CallbackContext> callback)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (!binding.Key.Equals(key) || binding.Callback != callback) continue;

                if (binding.Applied != null) Unsubscribe(binding);
                else                         _pendingCount--; // 尚未生效：只是从待生效队列里去掉
                _bindings.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool UnbindPerformed(ActionKey key, Action onPerformed)
        {
            var wrapperKey = (key, onPerformed);
            if (!_performedWrappers.TryGetValue(wrapperKey, out var wrapper)) return false;

            _performedWrappers.Remove(wrapperKey);
            return Unbind(key, wrapper);
        }

        public void UnbindAll()
        {
            for (int i = 0; i < _bindings.Count; i++)
                if (_bindings[i].Applied != null) Unsubscribe(_bindings[i]);

            _bindings.Clear();
            _performedWrappers.Clear();
            _warnedDisabledMaps.Clear();
            _pendingCount = 0;
        }

        #endregion

        #region 订阅 / 重试

        /// <summary>尝试把一条绑定真正挂到 InputAction 上。输入源或 Action 不可得时返回 false，留待重试。</summary>
        private bool TrySubscribe(Binding binding)
        {
            var asset = ResolveActions();
            if (!asset) return false;

            var map = asset.FindActionMap(binding.Key.Map, false);
            var action = map?.FindAction(binding.Key.Action, false);
            if (action == null) return false;

            // 三个阶段全订阅：调用方常靠 started / performed 判「按下」、canceled 判「抬起」
            // （典型写法是在回调里 ctx.ReadValue<float>() 比对 0 / 1），只订 performed 会收不到抬起。
            action.started   += binding.Callback;
            action.performed += binding.Callback;
            action.canceled  += binding.Callback;
            binding.Applied = action;

            WarnIfMapDisabled(action);
            return true;
        }

        private static void Unsubscribe(Binding binding)
        {
            var action = binding.Applied;
            binding.Applied = null;
            if (action == null) return;

            action.started   -= binding.Callback;
            action.performed -= binding.Callback;
            action.canceled  -= binding.Callback;
        }

        private void LateUpdate()
        {
            if (_pendingCount <= 0) return;

            // 输入源仍不可得（PlayerInput 尚未生成）：保留挂起，下一帧再试。
            if (!ResolveActions()) return;

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding.Applied != null) continue;
                if (TrySubscribe(binding)) _pendingCount--;
            }
        }

        protected override void OnDestroy()
        {
            // InputActionAsset 是 ScriptableObject 资源，在编辑器里跨播放会话存活：
            // 不退订就会把委托留在资源的 Action 上，下次播放时重复触发、且指向已销毁的对象。
            UnbindAll();
            base.OnDestroy();
        }

        #endregion

        #region 诊断

        /// <summary>
        /// 绑定到一个当前处于禁用状态的 ActionMap 时给出一次警告——这种情况下回调永远不会触发，
        /// 而 Input System 本身不会有任何提示，极难排查。每个 Map 只警告一次。
        /// </summary>
        private void WarnIfMapDisabled(InputAction action)
        {
            var map = action.actionMap;
            if (map == null || map.enabled) return;
            if (!_warnedDisabledMaps.Add(map.name)) return;

            Debug.LogWarning(
                $"[Toolkit.Input] 已绑定到 '{map.name}/{action.name}'，但 ActionMap '{map.name}' 当前处于禁用状态，回调不会触发。\n" +
                "本绑定器只负责接线、不改动输入的启用状态（启停交由 PlayerInput 之类的输入状态机统一决定）——\n" +
                $"请把 PlayerInput 的 Default Map 设为 '{map.name}'，或自行调用 SwitchCurrentActionMap / map.Enable()。");
        }

        #endregion
    }
}
