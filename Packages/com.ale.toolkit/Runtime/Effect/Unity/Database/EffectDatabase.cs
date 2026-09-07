using System.Collections.Generic;
using Ale.GameplayTags;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Effect
{
    /// <summary>
    /// 效果库（ScriptableObject，项目级可多份）：所有上层系统共用的效果配置——枚举类型（自定义属性用）、效果模板、
    /// 效果条目、Gameplay 标签声明。由 toolkit 的 Effect Editor 统一编辑；上层系统只以效果 id 引用，并按 toolkit 契约实现各自的执行器。
    /// 运行时经 <see cref="EffectDataManager"/> 注册（或放在 <c>Resources</c> 下随 <see cref="EffectRuntime"/> 自动注册）。
    /// </summary>
    [CreateAssetMenu(menuName = "Ale/Effect/Effect Database", fileName = "EffectDatabase")]
    public class EffectDatabase : ScriptableObject, IEnumTypeSource, IEffectSchemaSource, IEffectDefinitionSource
    {
        [SerializeField] private List<EnumType>              enumTypes       = new List<EnumType>();
        [SerializeField] private List<EffectTemplate>        effectTemplates = new List<EffectTemplate>();
        [SerializeField] private List<EffectEntry>           effects         = new List<EffectEntry>();
        [SerializeField] private List<GameplayTagDefinition> gameplayTags    = new List<GameplayTagDefinition>();

        #region 访问器

        /// <summary>枚举类型 列表（自定义属性的枚举字段用）。</summary>
        public List<EnumType> EnumTypesList => enumTypes;

        /// <summary>效果模板 列表。</summary>
        public List<EffectTemplate> EffectTemplates => effectTemplates;

        /// <summary>效果条目 列表。</summary>
        public List<EffectEntry> Effects => effects;

        /// <summary>本库声明的 Gameplay 标签（注册时并入 toolkit 标签注册表，供编辑器下拉与校验；运行时匹配不依赖它）。</summary>
        public List<GameplayTagDefinition> GameplayTags => gameplayTags;

        IReadOnlyList<EnumType> IEnumTypeSource.EnumTypes => enumTypes;

        #endregion

        #region 查询

        /// <summary>按名称查找枚举类型，未找到返回 null。</summary>
        public EnumType GetEnumType(string enumName) => Find(enumTypes, enumName, e => e.name);

        /// <summary>按名称查找效果模板，未找到返回 null。</summary>
        public EffectTemplate GetTemplate(string templateName) => Find(effectTemplates, templateName, t => t.name);

        /// <summary>按 id 查找效果条目，未找到返回 null。</summary>
        public EffectEntry GetEffect(string effectId) => Find(effects, effectId, e => e.id);

        // 显式实现 IEffectDefinitionSource：供 toolkit 效果容器 / EffectApplier 按 id 取定义。
        EffectDefinition IEffectDefinitionSource.GetEffect(string id) => GetEffect(id)?.definition;

        private static T Find<T>(List<T> list, string key, System.Func<T, string> keyOf) where T : class
        {
            if (string.IsNullOrEmpty(key) || list == null) return null;
            foreach (var item in list)
                if (item != null && keyOf(item) == key) return item;
            return null;
        }

        #endregion

        #region 维护

        /// <summary>归一全部条目与模板（幂等）：同步定义 id / 显示名、补 null、空阶段改写。</summary>
        public void NormalizeAll()
        {
            foreach (var t in effectTemplates) t?.Normalize();
            foreach (var e in effects)         e?.Normalize();
        }

        /// <summary>按模板 schema 对账全部效果的自定义属性值（幂等）。</summary>
        public void RebuildAllAttributes()
        {
            foreach (var e in effects) e?.RebuildAttributes(this);
        }

        /// <summary>添加枚举类型（同名已存在则忽略）。</summary>
        public void AddEnumType(string enumName, params string[] itemNames)
        {
            if (GetEnumType(enumName) != null) return;
            var enumType = new EnumType(enumName);
            enumType.SeedItems(itemNames);
            enumTypes.Add(enumType);
        }

        /// <summary>用另一个效果库的全部数据深拷贝覆盖自身（「以此为模板」新建数据文件用）。</summary>
        public void CloneFrom(EffectDatabase source)
        {
            if (!source) return;
            enumTypes.Clear();       foreach (var e in source.enumTypes)       if (e != null) enumTypes.Add(e.Clone());
            effectTemplates.Clear(); foreach (var t in source.effectTemplates) if (t != null) effectTemplates.Add(t.Clone());
            effects.Clear();         foreach (var e in source.effects)         if (e != null) effects.Add(e.Clone());
            gameplayTags.Clear();    foreach (var t in source.gameplayTags)    if (t != null) gameplayTags.Add(t.Clone());
        }

        #endregion

        #region 校验

        /// <summary>
        /// 校验：枚举 / 模板 / 效果的重复键；效果的模板引用悬空；效果定义错误（对「已同步 id / 显示名并归一」的副本校验，
        /// toolkit 的「警告:」不阻断）；Gameplay 标签名合法。返回是否无错误。
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            CheckDuplicates(enumTypes,       e => e.name, "枚举类型 name", errors);
            CheckDuplicates(effectTemplates, t => t.name, "效果模板 name", errors);
            CheckDuplicates(effects,         e => e.id,   "效果 id",       errors);

            var dangling = new List<string>();
            foreach (var e in effects)
            {
                if (e == null) continue;
                if (!string.IsNullOrEmpty(e.templateRef) && GetTemplate(e.templateRef) == null)
                    dangling.Add($"效果[{e.id}].templateRef → 效果模板 '{e.templateRef}'");

                if (e.definition == null) { errors.Add($"效果[{e.id}]：定义为空"); continue; }
                var def = e.definition.Clone();
                def.id          = e.id;
                def.displayName = e.PlainName();
                def.Normalize();
                var messages = new List<string>();
                def.Validate(messages);
                foreach (var m in messages)
                    if (!EffectDefinition.IsWarning(m)) errors.Add(m);
            }
            if (dangling.Count > 0)
                errors.Add("存在悬空引用：" + string.Join("；", dangling));

            for (int i = 0; i < gameplayTags.Count; i++)
            {
                var t = gameplayTags[i];
                if (t != null && !GameplayTag.IsValidName(t.name))
                    errors.Add($"Gameplay 标签[{i}] '{t.name}' 不合法（段不能为空，段内不能含空白或 '/'）");
            }

            return errors.Count == 0;
        }

        private static void CheckDuplicates<T>(List<T> list, System.Func<T, string> keyOf, string noun, List<string> errors) where T : class
        {
            var seen = new HashSet<string>();
            var dup  = new HashSet<string>();
            foreach (var item in list)
            {
                if (item == null) continue;
                string key = keyOf(item);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!seen.Add(key)) dup.Add(key);
            }
            if (dup.Count > 0)
                errors.Add($"存在重复的{noun}：{string.Join(", ", dup)}");
        }

        #endregion
    }
}
