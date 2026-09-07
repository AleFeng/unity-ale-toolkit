using System.IO;
using System.Text;
using Ale.GameplayTags;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Runtime.Serialization;
using UnityEngine;
using static Ale.Toolkit.Runtime.Serialization.ToolkitBinaryCodec;

namespace Ale.Effect.Serialization
{
    /// <summary>
    /// 效果库序列化器：<see cref="EffectDatabase"/> ↔ JSON（<see cref="JsonUtility"/>，定义直接内嵌）/ 紧凑二进制（魔数 + 版本头；
    /// 定义以 <see cref="EffectJson"/> 串承载，属性值经 <see cref="ToolkitBinaryCodec"/>）。单向导出格式；对象引用经
    /// <see cref="IAssetRefResolver"/> 转 GUID（运行时用 <see cref="NullAssetRefResolver"/>，图标不解析、仅保留 GUID / 地址）。
    /// 版本按「尾部追加块」向后兼容（当前 v1 尚无追加块，扩展点已就位）。
    /// </summary>
    public static class EffectConfigSerializer
    {
        /// <summary>魔数 "EFDB"。</summary>
        private const int Magic = 0x45464442;

        /// <summary>当前二进制格式版本。</summary>
        public const int Version = 1;

        #region 映射：运行时 ↔ DTO

        public static EffectDatabaseDto ToDto(EffectDatabase db, IAssetRefResolver resolver)
        {
            resolver ??= NullAssetRefResolver.Instance;
            return new EffectDatabaseDto
            {
                version         = Version,
                enumTypes       = ToolkitDtoMapper.ToArray(db.EnumTypesList, e => ToDto(e, resolver)),
                effectTemplates = ToolkitDtoMapper.ToArray(db.EffectTemplates, t => ToDto(t, resolver)),
                effects         = ToolkitDtoMapper.ToArrayFiltered(db.Effects,
                                      e => e != null && !string.IsNullOrWhiteSpace(e.id), e => ToDto(e, resolver)),
                gameplayTags    = ToolkitDtoMapper.ToArrayFiltered(db.GameplayTags,
                                      t => t != null && !string.IsNullOrWhiteSpace(t.name), t => t.Clone()),
            };
        }

        /// <summary>把 DTO 写入既有效果库（先清空其内容）。</summary>
        public static void FromDto(EffectDatabaseDto dto, EffectDatabase target, IAssetRefResolver resolver)
        {
            resolver ??= NullAssetRefResolver.Instance;
            target.EnumTypesList.Clear();
            target.EffectTemplates.Clear();
            target.Effects.Clear();
            target.GameplayTags.Clear();
            if (dto == null) return;

            if (dto.enumTypes != null)       foreach (var e in dto.enumTypes)       if (e != null) target.EnumTypesList.Add(FromDto(e, resolver));
            if (dto.effectTemplates != null) foreach (var t in dto.effectTemplates) if (t != null) target.EffectTemplates.Add(FromDto(t, resolver));
            if (dto.effects != null)         foreach (var e in dto.effects)         if (e != null) target.Effects.Add(FromDto(e, resolver));
            if (dto.gameplayTags != null)    foreach (var t in dto.gameplayTags)    if (t != null && !string.IsNullOrEmpty(t.name)) target.GameplayTags.Add(t.Clone());
        }

        private static EnumTypeDto ToDto(EnumType e, IAssetRefResolver resolver)
        {
            return new EnumTypeDto
            {
                name       = e.name,
                nextValue  = e.nextValue,
                attributes = ToolkitDtoMapper.ToArray(e.attributes, a => ToolkitDtoMapper.ToDto(a, resolver)),
                items      = ToolkitDtoMapper.ToArray(e.items, it => new EnumItemDto
                {
                    name            = it.name,
                    value           = it.value,
                    attributeValues = ToolkitDtoMapper.ToDto(it.attributeValues, resolver),
                }),
            };
        }

        private static EnumType FromDto(EnumTypeDto dto, IAssetRefResolver resolver)
        {
            var e = new EnumType(dto.name) { nextValue = dto.nextValue };
            if (dto.attributes != null)
                foreach (var a in dto.attributes)
                    e.attributes.Add(ToolkitDtoMapper.FromDto(a, resolver));
            if (dto.items != null)
                foreach (var it in dto.items)
                {
                    var item = new EnumItem(it.name, it.value);
                    ToolkitDtoMapper.FromDto(it.attributeValues, item.attributeValues, resolver);
                    e.items.Add(item);
                }
            return e;
        }

        private static EffectTemplateDto ToDto(EffectTemplate t, IAssetRefResolver resolver)
        {
            var dto = new EffectTemplateDto { defaultDefinition = CloneNormalized(t.defaultDefinition) };
            ToolkitDtoMapper.FillTemplateDto(dto, t, resolver);   // 名称 / 色点 / 属性字段
            return dto;
        }

        private static EffectTemplate FromDto(EffectTemplateDto dto, IAssetRefResolver resolver)
        {
            var t = new EffectTemplate { defaultDefinition = CloneNormalized(dto.defaultDefinition) };
            ToolkitDtoMapper.FillTemplate(t, dto, resolver);
            return t;
        }

        private static EffectEntryDto ToDto(EffectEntry e, IAssetRefResolver resolver)
        {
            var def = CloneNormalized(e.definition);
            def.id          = e.id;
            def.displayName = e.PlainName();
            return new EffectEntryDto
            {
                id              = e.id,
                templateRef     = e.templateRef,
                displayText     = ToolkitDtoMapper.ToDto(e.displayText, resolver),
                descriptionText = ToolkitDtoMapper.ToDto(e.descriptionText, resolver),
                iconValue       = ToolkitDtoMapper.ToDto(e.iconValue, resolver),
                values          = ToolkitDtoMapper.ToDto(e.values, resolver),
                definition      = def,
            };
        }

        private static EffectEntry FromDto(EffectEntryDto dto, IAssetRefResolver resolver)
        {
            var e = new EffectEntry
            {
                id              = dto.id,
                templateRef     = dto.templateRef,
                displayText     = ToolkitDtoMapper.TextFromDto(dto.displayText, resolver),
                descriptionText = ToolkitDtoMapper.TextFromDto(dto.descriptionText, resolver),
                iconValue       = dto.iconValue != null ? ToolkitDtoMapper.FromDto(dto.iconValue, resolver) : new AttributeValue(EFieldType.Sprite),
                definition      = CloneNormalized(dto.definition),
            };
            ToolkitDtoMapper.FromDto(dto.values, e.values, resolver);
            e.Normalize();
            return e;
        }

        /// <summary>深拷贝并归一（不改动配置本体；null → 新定义）。</summary>
        private static EffectDefinition CloneNormalized(EffectDefinition def)
        {
            var c = def != null ? def.Clone() : new EffectDefinition();
            c.Normalize();
            return c;
        }

        #endregion

        #region JSON

        /// <summary>导出为 JSON（定义直接内嵌）。</summary>
        public static string ExportJson(EffectDatabase db, IAssetRefResolver resolver = null)
            => JsonUtility.ToJson(ToDto(db, resolver), true);

        /// <summary>从 JSON 导入为一个新的 <see cref="EffectDatabase"/> 实例。</summary>
        public static EffectDatabase ImportJson(string json, IAssetRefResolver resolver = null)
        {
            var db = ScriptableObject.CreateInstance<EffectDatabase>();
            ImportJsonInto(json, db, resolver);
            return db;
        }

        /// <summary>把 JSON 导入到既有效果库（先清空其内容）。</summary>
        public static void ImportJsonInto(string json, EffectDatabase target, IAssetRefResolver resolver = null)
        {
            if (string.IsNullOrEmpty(json) || !target) return;
            FromDto(JsonUtility.FromJson<EffectDatabaseDto>(json), target, resolver);
        }

        #endregion

        #region 二进制

        /// <summary>导出为紧凑二进制。</summary>
        public static byte[] Export(EffectDatabase db, IAssetRefResolver resolver = null)
        {
            var dto = ToDto(db, resolver);
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write(Version);
                WriteArray(w, dto.enumTypes, WriteEnumType);
                WriteArray(w, dto.effectTemplates, WriteTemplate);
                WriteArray(w, dto.effects, WriteEntry);
                WriteArray(w, dto.gameplayTags, WriteTag);
            }
            return stream.ToArray();
        }

        /// <summary>从二进制导入为一个新的 <see cref="EffectDatabase"/> 实例。</summary>
        public static EffectDatabase Import(byte[] bytes, IAssetRefResolver resolver = null)
        {
            var db = ScriptableObject.CreateInstance<EffectDatabase>();
            ImportInto(bytes, db, resolver);
            return db;
        }

        /// <summary>把二进制导入到既有效果库（先清空其内容）。魔数不匹配时报错并保持目标不变。</summary>
        public static void ImportInto(byte[] bytes, EffectDatabase target, IAssetRefResolver resolver = null)
        {
            if (bytes == null || bytes.Length < 8 || !target) return;

            using var stream = new MemoryStream(bytes);
            using var r = new BinaryReader(stream, Encoding.UTF8);

            if (r.ReadInt32() != Magic)
            {
                Debug.LogError("[EffectConfigSerializer] 魔数不匹配，数据格式无效。");
                return;
            }
            int version = r.ReadInt32();
            if (version > Version)
                Debug.LogWarning($"[EffectConfigSerializer] 文件版本（{version}）高于当前支持（{Version}），尝试按当前格式解析。");

            var dto = new EffectDatabaseDto
            {
                version         = version,
                enumTypes       = ReadArray(r, ReadEnumType),
                effectTemplates = ReadArray(r, ReadTemplate),
                effects         = ReadArray(r, ReadEntry),
                gameplayTags    = ReadArray(r, ReadTag),
            };
            FromDto(dto, target, resolver);
        }

        private static void WriteTemplate(BinaryWriter w, EffectTemplateDto t)
        {
            WriteStr(w, t.name);
            WriteFloatArray(w, t.color);
            WriteArray(w, t.attributes, WriteDefinition);
            WriteStr(w, t.defaultDefinition != null ? EffectJson.ToJson(t.defaultDefinition, false) : string.Empty);
        }

        private static EffectTemplateDto ReadTemplate(BinaryReader r)
        {
            var t = new EffectTemplateDto
            {
                name       = ReadStr(r),
                color      = ReadFloatArray(r),
                attributes = ReadArray(r, ReadDefinition),
            };
            string json = ReadStr(r);
            t.defaultDefinition = string.IsNullOrEmpty(json) ? null : EffectJson.DefinitionFromJson(json);
            return t;
        }

        private static void WriteEntry(BinaryWriter w, EffectEntryDto e)
        {
            WriteStr(w, e.id);
            WriteStr(w, e.templateRef);
            WriteValue(w, e.displayText);
            WriteValue(w, e.descriptionText);
            WriteValue(w, e.iconValue);
            WriteEntries(w, e.values);
            WriteStr(w, e.definition != null ? EffectJson.ToJson(e.definition, false) : string.Empty);
        }

        private static EffectEntryDto ReadEntry(BinaryReader r)
        {
            var e = new EffectEntryDto
            {
                id              = ReadStr(r),
                templateRef     = ReadStr(r),
                displayText     = ReadValue(r),
                descriptionText = ReadValue(r),
                iconValue       = ReadValue(r),
                values          = ReadEntries(r),
            };
            string json = ReadStr(r);
            e.definition = string.IsNullOrEmpty(json) ? null : EffectJson.DefinitionFromJson(json);
            return e;
        }

        private static void WriteTag(BinaryWriter w, GameplayTagDefinition t)
        {
            WriteStr(w, t?.name);
            WriteStr(w, t?.comment);
        }

        private static GameplayTagDefinition ReadTag(BinaryReader r)
        {
            string name    = ReadStr(r);
            string comment = ReadStr(r);
            return new GameplayTagDefinition(name, comment);
        }

        #endregion
    }
}
