using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Condition
{
    /// <summary>
    /// 条件库（ScriptableObject，项目级可多份）：所有上层系统共用的具名条件配置——枚举类型（自定义属性用）、
    /// 条件模板、条件条目。由 toolkit 的 Condition Editor 统一编辑；上层系统只以条件 id 引用，并按 toolkit 契约实现各自的判定器。
    /// 运行时经 <see cref="ConditionDataManager"/> 注册（或放在 <c>Resources</c> 下随 <see cref="ConditionRuntime"/> 自动注册）。
    ///
    /// <para>本库<b>不持有 Gameplay 标签</b>——标签由效果库 <c>EffectDatabase</c> 统一声明并注册进 toolkit 标签注册表，
    /// 条件侧只经 <c>Condition.HasGameplayTag</c> / <c>Condition.GameplayTags</c> 两个判定器读取。</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Ale/Condition/Condition Database", fileName = "ConditionDatabase")]
    public class ConditionDatabase : ScriptableObject, IEnumTypeSource, IConditionSchemaSource, IConditionDefinitionSource
    {
        [SerializeField] private List<EnumType>          enumTypes          = new List<EnumType>();
        [SerializeField] private List<ConditionTemplate> conditionTemplates = new List<ConditionTemplate>();
        [SerializeField] private List<ConditionEntry>    conditions         = new List<ConditionEntry>();

        #region 访问器

        /// <summary>枚举类型 列表（自定义属性的枚举字段用）。</summary>
        public List<EnumType> EnumTypesList => enumTypes;

        /// <summary>条件模板 列表。</summary>
        public List<ConditionTemplate> ConditionTemplates => conditionTemplates;

        /// <summary>条件条目 列表。</summary>
        public List<ConditionEntry> Conditions => conditions;

        IReadOnlyList<EnumType> IEnumTypeSource.EnumTypes => enumTypes;

        #endregion

        #region 查询

        /// <summary>按名称查找枚举类型，未找到返回 null。</summary>
        public EnumType GetEnumType(string enumName) => Find(enumTypes, enumName, e => e.name);

        /// <summary>按名称查找条件模板，未找到返回 null。</summary>
        public ConditionTemplate GetTemplate(string templateName) => Find(conditionTemplates, templateName, t => t.name);

        /// <summary>按 id 查找条件条目，未找到返回 null。</summary>
        public ConditionEntry GetEntry(string conditionId) => Find(conditions, conditionId, c => c.id);

        // 显式实现 IConditionDefinitionSource：供 ConditionResolver 按 id 取表达式。
        ConditionExpression IConditionDefinitionSource.GetCondition(string id) => GetEntry(id)?.expression;

        private static T Find<T>(List<T> list, string key, System.Func<T, string> keyOf) where T : class
        {
            if (string.IsNullOrEmpty(key) || list == null) return null;
            foreach (var item in list)
                if (item != null && keyOf(item) == key) return item;
            return null;
        }

        #endregion

        #region 维护

        /// <summary>归一全部条目与模板（幂等）：补 null、剔除 null 组 / 项。</summary>
        public void NormalizeAll()
        {
            foreach (var t in conditionTemplates) t?.Normalize();
            foreach (var c in conditions)         c?.Normalize();
        }

        /// <summary>按模板 schema 对账全部条件的自定义属性值（幂等）。</summary>
        public void RebuildAllAttributes()
        {
            foreach (var c in conditions) c?.RebuildAttributes(this);
        }

        /// <summary>添加枚举类型（同名已存在则忽略）。</summary>
        public void AddEnumType(string enumName, params string[] itemNames)
        {
            if (GetEnumType(enumName) != null) return;
            var enumType = new EnumType(enumName);
            enumType.SeedItems(itemNames);
            enumTypes.Add(enumType);
        }

        /// <summary>用另一个条件库的全部数据深拷贝覆盖自身（「以此为模板」新建数据文件用）。</summary>
        public void CloneFrom(ConditionDatabase source)
        {
            if (!source) return;
            enumTypes.Clear();          foreach (var e in source.enumTypes)          if (e != null) enumTypes.Add(e.Clone());
            conditionTemplates.Clear(); foreach (var t in source.conditionTemplates) if (t != null) conditionTemplates.Add(t.Clone());
            conditions.Clear();         foreach (var c in source.conditions)         if (c != null) conditions.Add(c.Clone());
        }

        #endregion

        #region 校验

        /// <summary>
        /// 校验：枚举 / 模板 / 条件的重复键；条件的模板引用悬空；表达式为空；条件项未选择判定器
        /// （空 key 在 <see cref="ConditionEngine"/> 里恒判不通过，属半配置状态）。返回是否无错误。
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            CheckDuplicates(enumTypes,          e => e.name, "枚举类型 name", errors);
            CheckDuplicates(conditionTemplates, t => t.name, "条件模板 name", errors);
            CheckDuplicates(conditions,         c => c.id,   "条件 id",       errors);

            var dangling = new List<string>();
            foreach (var c in conditions)
            {
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.templateRef) && GetTemplate(c.templateRef) == null)
                    dangling.Add($"条件[{c.id}].templateRef → 条件模板 '{c.templateRef}'");

                if (c.expression == null) { errors.Add($"条件[{c.id}]：表达式为空"); continue; }

                var groups = c.expression.groups;
                if (groups == null) continue;
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var items = groups[gi]?.items;
                    if (items == null) continue;
                    for (int ii = 0; ii < items.Count; ii++)
                        if (items[ii] != null && string.IsNullOrEmpty(items[ii].key))
                            errors.Add($"条件[{c.id}] 组{gi + 1} 第{ii + 1}项：未选择判定器（空键恒判不通过）");
                }
            }
            if (dangling.Count > 0)
                errors.Add("存在悬空引用：" + string.Join("；", dangling));

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
