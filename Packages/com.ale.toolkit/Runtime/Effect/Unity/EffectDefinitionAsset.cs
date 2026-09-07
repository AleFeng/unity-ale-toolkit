using System.Collections.Generic;
using UnityEngine;

namespace Ale.Effect
{
    /// <summary>
    /// 可选的效果定义 SO 容器：把一份 <see cref="EffectDefinition"/>（GAS GameplayEffect）持久化为独立资产，供纯 toolkit 用户
    /// 或原型使用；有自己数据库的宿主（角色 / 道具系统）通常把定义放在数据库顶层列表、以 id 引用，不用本资产。
    /// Inspector 由 <c>EffectDefinitionDrawer</c> 分节绘制，并带「校验 / 归一」按钮。
    /// </summary>
    /// <remarks>层级约定：此 SO 字段遵循仓库常规「private + <c>[SerializeField]</c>」。</remarks>
    [CreateAssetMenu(menuName = "Ale/Effect/Effect Definition", fileName = "EffectDefinition")]
    public class EffectDefinitionAsset : ScriptableObject
    {
        [SerializeField] private EffectDefinition definition = new EffectDefinition();

        /// <summary>效果定义（编辑器直接改动；运行时读取）。</summary>
        public EffectDefinition Definition => definition;

        /// <summary>转调 <see cref="EffectDefinition.Validate"/>。</summary>
        public bool Validate(List<string> messages) => definition != null && definition.Validate(messages);

        /// <summary>转调 <see cref="EffectDefinition.Normalize"/>（补 null / 归一标签 / 空阶段改写为 onApply）。</summary>
        public void Normalize()
        {
            definition ??= new EffectDefinition();
            definition.Normalize();
        }
    }
}
