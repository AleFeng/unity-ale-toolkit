using System;
using System.Collections.Generic;
using Ale.Effect.Serialization;
using Ale.GameplayTags;
using Ale.Toolkit.Runtime;

namespace Ale.Effect
{
    /// <summary>
    /// 效果数据管理器（<see cref="ToolkitSingleton{T}"/>）：注册一个或多个 <see cref="EffectDatabase"/>，提供跨库 O(1) 惰性索引查询，
    /// 并把自身登记为全局 <see cref="EffectDefinitionRegistry.Default"/> 的来源（任何系统按 id 施加效果时可解析），
    /// 把各库声明的 Gameplay 标签并入 toolkit 标签注册表。id 冲突「先注册者优先」。
    ///
    /// <para>登记注册表来源发生在 <see cref="Register"/>（幂等）而非构造时：<see cref="EffectRuntime"/> 在 SubsystemRegistration 阶段
    /// 会清空注册表，先于任何注册；关闭 Domain Reload 时单例本身也随 toolkit 单例登记表复位。</para>
    /// </summary>
    public class EffectDataManager : ToolkitSingleton<EffectDataManager>, IEffectDefinitionSource, IEnumTypeSource, IEffectSchemaSource
    {
        private readonly List<EffectDatabase> _databases = new List<EffectDatabase>();
        private bool _registeredAsSource;

        /// <summary>已注册的效果库列表。</summary>
        public IReadOnlyList<EffectDatabase> Databases => _databases;

        protected override void Init() { }

        #region 注册 / 加载

        /// <summary>注册一个效果库（去重）：逐条归一、置脏索引、登记为全局定义源、并入 Gameplay 标签。</summary>
        public void Register(EffectDatabase database)
        {
            if (!database || _databases.Contains(database)) return;
            database.NormalizeAll();
            _databases.Add(database);
            InvalidateIndex();

            if (!_registeredAsSource)
            {
                EffectDefinitionRegistry.Default.AddSource(this);
                _registeredAsSource = true;
            }
            GameplayTagRuntime.Register(database.GameplayTags);
        }

        /// <summary>注销一个效果库（最后一个注销后从全局定义源移除）。</summary>
        public void Unregister(EffectDatabase database)
        {
            if (!database) return;
            if (_databases.Remove(database)) InvalidateIndex();
            if (_databases.Count == 0) RemoveFromRegistry();
        }

        /// <summary>清空所有已注册效果库（并从全局定义源移除）。</summary>
        public void ClearDatabases()
        {
            _databases.Clear();
            InvalidateIndex();
            RemoveFromRegistry();
        }

        /// <summary>从二进制反序列化为一个新的 <see cref="EffectDatabase"/> 并注册（对象引用不解析，仅保留 GUID / 地址）。</summary>
        public EffectDatabase LoadFromBinary(byte[] bytes)
        {
            var db = EffectConfigSerializer.Import(bytes, null);
            Register(db);
            return db;
        }

        /// <summary>从 JSON 反序列化为一个新的 <see cref="EffectDatabase"/> 并注册。</summary>
        public EffectDatabase LoadFromJson(string json)
        {
            var db = EffectConfigSerializer.ImportJson(json, null);
            Register(db);
            return db;
        }

        private void RemoveFromRegistry()
        {
            if (!_registeredAsSource) return;
            EffectDefinitionRegistry.Default.RemoveSource(this);
            _registeredAsSource = false;
        }

        #endregion

        #region 查询索引

        private bool _indexDirty = true;

        private readonly Dictionary<string, EffectEntry>    _effects   = new Dictionary<string, EffectEntry>();
        private readonly Dictionary<string, EffectTemplate> _templates = new Dictionary<string, EffectTemplate>();
        private readonly Dictionary<string, EnumType>       _enumTypes = new Dictionary<string, EnumType>();
        private readonly List<EnumType>                     _enumList  = new List<EnumType>();

        /// <summary>使查询索引失效，下次查询时重建。运行期直接改动已注册库内容后需手动调用。</summary>
        public void InvalidateIndex() => _indexDirty = true;

        private void EnsureIndex()
        {
            if (!_indexDirty) return;
            _indexDirty = false;

            _effects.Clear(); _templates.Clear(); _enumTypes.Clear(); _enumList.Clear();
            foreach (var db in _databases)
            {
                if (!db) continue;
                Index(_effects,   db.Effects,         x => x.id);
                Index(_templates, db.EffectTemplates, x => x.name);
                Index(_enumTypes, db.EnumTypesList,   x => x.name);
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

        /// <summary>按 id 跨库查找效果条目，未找到返回 null。</summary>
        public EffectEntry GetEffect(string effectId) => Lookup(_effects, effectId);

        // 显式实现 IEffectDefinitionSource：全局定义注册表 / toolkit EffectApplier 按 id 取定义。
        EffectDefinition IEffectDefinitionSource.GetEffect(string id) => GetEffect(id)?.definition;

        /// <summary>按名称跨库查找效果模板，未找到返回 null。</summary>
        public EffectTemplate GetTemplate(string templateName) => Lookup(_templates, templateName);

        /// <summary>按名称跨库查找枚举类型，未找到返回 null。</summary>
        public EnumType GetEnumType(string enumName) => Lookup(_enumTypes, enumName);

        /// <summary>全部枚举类型（跨库去重，先注册先得）。</summary>
        public IReadOnlyList<EnumType> EnumTypes
        {
            get { EnsureIndex(); return _enumList; }
        }

        /// <summary>跨库返回全部效果条目（id 去重、先注册先得）；未注册返回空列表。</summary>
        public List<EffectEntry> GetAllEffects()
        {
            EnsureIndex();
            return new List<EffectEntry>(_effects.Values);
        }

        #endregion
    }
}
