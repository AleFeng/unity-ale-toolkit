using System;
using System.Collections.Generic;
using Ale.Condition.Serialization;
using Ale.Toolkit.Runtime;

namespace Ale.Condition
{
    /// <summary>
    /// 条件数据管理器（<see cref="ToolkitSingleton{T}"/>）：注册一个或多个 <see cref="ConditionDatabase"/>，提供跨库 O(1) 惰性索引查询，
    /// 并把自身登记为全局 <see cref="ConditionDefinitionRegistry.Default"/> 的来源（任何系统按 id 求值条件时可解析）。
    /// id 冲突「先注册者优先」。
    ///
    /// <para>登记注册表来源发生在 <see cref="Register"/>（幂等）而非构造时：<see cref="ConditionRuntime"/> 在 SubsystemRegistration 阶段
    /// 会清空注册表，先于任何注册；关闭 Domain Reload 时单例本身也随 toolkit 单例登记表复位。</para>
    /// </summary>
    public class ConditionDataManager : ToolkitSingleton<ConditionDataManager>,
        IConditionDefinitionSource, IEnumTypeSource, IConditionSchemaSource
    {
        private readonly List<ConditionDatabase> _databases = new List<ConditionDatabase>();
        private bool _registeredAsSource;

        /// <summary>已注册的条件库列表。</summary>
        public IReadOnlyList<ConditionDatabase> Databases => _databases;

        protected override void Init() { }

        #region 注册 / 加载

        /// <summary>注册一个条件库（去重）：逐条归一、置脏索引、登记为全局条件源。</summary>
        public void Register(ConditionDatabase database)
        {
            if (!database || _databases.Contains(database)) return;
            database.NormalizeAll();
            _databases.Add(database);
            InvalidateIndex();

            if (!_registeredAsSource)
            {
                ConditionDefinitionRegistry.Default.AddSource(this);
                _registeredAsSource = true;
            }
        }

        /// <summary>注销一个条件库（最后一个注销后从全局条件源移除）。</summary>
        public void Unregister(ConditionDatabase database)
        {
            if (!database) return;
            if (_databases.Remove(database)) InvalidateIndex();
            if (_databases.Count == 0) RemoveFromRegistry();
        }

        /// <summary>清空所有已注册条件库（并从全局条件源移除）。</summary>
        public void ClearDatabases()
        {
            _databases.Clear();
            InvalidateIndex();
            RemoveFromRegistry();
        }

        /// <summary>从二进制反序列化为一个新的 <see cref="ConditionDatabase"/> 并注册（对象引用不解析，仅保留 GUID / 地址）。</summary>
        public ConditionDatabase LoadFromBinary(byte[] bytes)
        {
            var db = ConditionConfigSerializer.Import(bytes, null);
            Register(db);
            return db;
        }

        /// <summary>从 JSON 反序列化为一个新的 <see cref="ConditionDatabase"/> 并注册。</summary>
        public ConditionDatabase LoadFromJson(string json)
        {
            var db = ConditionConfigSerializer.ImportJson(json, null);
            Register(db);
            return db;
        }

        private void RemoveFromRegistry()
        {
            if (!_registeredAsSource) return;
            ConditionDefinitionRegistry.Default.RemoveSource(this);
            _registeredAsSource = false;
        }

        #endregion

        #region 查询索引

        private bool _indexDirty = true;

        private readonly Dictionary<string, ConditionEntry>    _conditions = new Dictionary<string, ConditionEntry>();
        private readonly Dictionary<string, ConditionTemplate> _templates  = new Dictionary<string, ConditionTemplate>();
        private readonly Dictionary<string, EnumType>          _enumTypes  = new Dictionary<string, EnumType>();
        private readonly List<EnumType>                        _enumList   = new List<EnumType>();

        /// <summary>使查询索引失效，下次查询时重建。运行期直接改动已注册库内容后需手动调用。</summary>
        public void InvalidateIndex() => _indexDirty = true;

        private void EnsureIndex()
        {
            if (!_indexDirty) return;
            _indexDirty = false;

            _conditions.Clear(); _templates.Clear(); _enumTypes.Clear(); _enumList.Clear();
            foreach (var db in _databases)
            {
                if (!db) continue;
                Index(_conditions, db.Conditions,         x => x.id);
                Index(_templates,  db.ConditionTemplates, x => x.name);
                Index(_enumTypes,  db.EnumTypesList,      x => x.name);
            }
            _enumList.AddRange(_enumTypes.Values);
        }

        // 已存在的键不覆盖 —— 「先注册的库优先」。
        private static void Index<TValue>(Dictionary<string, TValue> map, List<TValue> source, Func<TValue, string> keyOf)
        {
            if (source == null) return;
            foreach (var v in source)
            {
                if (v == null) continue;
                string key = keyOf(v);
                if (string.IsNullOrEmpty(key) || map.ContainsKey(key)) continue;
                map[key] = v;
            }
        }

        private TValue Lookup<TValue>(Dictionary<string, TValue> map, string key)
        {
            if (string.IsNullOrEmpty(key)) return default;
            EnsureIndex();
            return map.TryGetValue(key, out var v) ? v : default;
        }

        #endregion

        #region 跨库查询

        /// <summary>按 id 跨库查找条件条目，未找到返回 null。</summary>
        public ConditionEntry GetEntry(string conditionId) => Lookup(_conditions, conditionId);

        // 显式实现 IConditionDefinitionSource：全局条件注册表 / ConditionResolver 按 id 取表达式。
        ConditionExpression IConditionDefinitionSource.GetCondition(string id) => GetEntry(id)?.expression;

        /// <summary>按名称跨库查找条件模板，未找到返回 null。</summary>
        public ConditionTemplate GetTemplate(string templateName) => Lookup(_templates, templateName);

        /// <summary>按名称跨库查找枚举类型，未找到返回 null。</summary>
        public EnumType GetEnumType(string enumName) => Lookup(_enumTypes, enumName);

        /// <summary>全部枚举类型（跨库去重，先注册先得）。</summary>
        public IReadOnlyList<EnumType> EnumTypes
        {
            get { EnsureIndex(); return _enumList; }
        }

        /// <summary>跨库返回全部条件条目（id 去重、先注册先得）；未注册返回空列表。</summary>
        public List<ConditionEntry> GetAllConditions()
        {
            EnsureIndex();
            return new List<ConditionEntry>(_conditions.Values);
        }

        #endregion
    }
}
