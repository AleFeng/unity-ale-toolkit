using System.IO;
using System.Text;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Runtime.Serialization;
using UnityEngine;
using static Ale.Toolkit.Runtime.Serialization.ToolkitBinaryCodec;

namespace Ale.Condition.Serialization
{
    /// <summary>
    /// 条件库序列化器：<see cref="ConditionDatabase"/> ↔ JSON（<see cref="JsonUtility"/>，表达式直接内嵌）/ 紧凑二进制
    /// （魔数 + 版本头；表达式以 <see cref="ConditionJson"/> 串承载，属性值经 <see cref="ToolkitBinaryCodec"/>）。
    /// 单向导出格式；对象引用经 <see cref="IAssetRefResolver"/> 转 GUID（运行时用 <see cref="NullAssetRefResolver"/>，
    /// 图标不解析、仅保留 GUID / 地址）。版本按「尾部追加块」向后兼容（当前 v1 尚无追加块，扩展点已就位）。
    /// 与效果侧的 <c>EffectConfigSerializer</c> 同构。
    /// </summary>
    public static class ConditionConfigSerializer
    {
        /// <summary>魔数 "CNDB"。</summary>
        private const int Magic = 0x434E4442;

        /// <summary>当前二进制格式版本。</summary>
        public const int Version = 1;

        #region 映射：运行时 ↔ DTO

        public static ConditionDatabaseDto ToDto(ConditionDatabase db, IAssetRefResolver resolver)
        {
            resolver ??= NullAssetRefResolver.Instance;
            return new ConditionDatabaseDto
            {
                version            = Version,
                enumTypes          = ToolkitDtoMapper.ToArray(db.EnumTypesList, e => ToDto(e, resolver)),
                conditionTemplates = ToolkitDtoMapper.ToArray(db.ConditionTemplates, t => ToDto(t, resolver)),
                conditions         = ToolkitDtoMapper.ToArrayFiltered(db.Conditions,
                                         c => c != null && !string.IsNullOrWhiteSpace(c.id), c => ToDto(c, resolver)),
            };
        }

        /// <summary>把 DTO 写入既有条件库（先清空其内容）。</summary>
        public static void FromDto(ConditionDatabaseDto dto, ConditionDatabase target, IAssetRefResolver resolver)
        {
            resolver ??= NullAssetRefResolver.Instance;
            target.EnumTypesList.Clear();
            target.ConditionTemplates.Clear();
            target.Conditions.Clear();
            if (dto == null) return;

            if (dto.enumTypes != null)          foreach (var e in dto.enumTypes)          if (e != null) target.EnumTypesList.Add(FromDto(e, resolver));
            if (dto.conditionTemplates != null) foreach (var t in dto.conditionTemplates) if (t != null) target.ConditionTemplates.Add(FromDto(t, resolver));
            if (dto.conditions != null)         foreach (var c in dto.conditions)         if (c != null) target.Conditions.Add(FromDto(c, resolver));
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

        private static ConditionTemplateDto ToDto(ConditionTemplate t, IAssetRefResolver resolver)
        {
            var dto = new ConditionTemplateDto { defaultExpression = CloneExpr(t.defaultExpression) };
            ToolkitDtoMapper.FillTemplateDto(dto, t, resolver);   // 名称 / 色点 / 属性字段
            return dto;
        }

        private static ConditionTemplate FromDto(ConditionTemplateDto dto, IAssetRefResolver resolver)
        {
            var t = new ConditionTemplate { defaultExpression = CloneExpr(dto.defaultExpression) };
            ToolkitDtoMapper.FillTemplate(t, dto, resolver);
            return t;
        }

        private static ConditionEntryDto ToDto(ConditionEntry c, IAssetRefResolver resolver)
        {
            return new ConditionEntryDto
            {
                id              = c.id,
                templateRef     = c.templateRef,
                displayText     = ToolkitDtoMapper.ToDto(c.displayText, resolver),
                descriptionText = ToolkitDtoMapper.ToDto(c.descriptionText, resolver),
                iconValue       = ToolkitDtoMapper.ToDto(c.iconValue, resolver),
                values          = ToolkitDtoMapper.ToDto(c.values, resolver),
                expression      = CloneExpr(c.expression),
            };
        }

        private static ConditionEntry FromDto(ConditionEntryDto dto, IAssetRefResolver resolver)
        {
            var c = new ConditionEntry
            {
                id              = dto.id,
                templateRef     = dto.templateRef,
                displayText     = ToolkitDtoMapper.TextFromDto(dto.displayText, resolver),
                descriptionText = ToolkitDtoMapper.TextFromDto(dto.descriptionText, resolver),
                iconValue       = dto.iconValue != null ? ToolkitDtoMapper.FromDto(dto.iconValue, resolver) : new AttributeValue(EFieldType.Sprite),
                expression      = CloneExpr(dto.expression),
            };
            ToolkitDtoMapper.FromDto(dto.values, c.values, resolver);
            c.Normalize();
            return c;
        }

        /// <summary>深拷贝（不改动配置本体；null → 新表达式）。</summary>
        private static ConditionExpression CloneExpr(ConditionExpression expr)
            => expr != null ? expr.Clone() : new ConditionExpression();

        #endregion

        #region JSON

        /// <summary>导出为 JSON（表达式直接内嵌）。</summary>
        public static string ExportJson(ConditionDatabase db, IAssetRefResolver resolver = null)
            => JsonUtility.ToJson(ToDto(db, resolver), true);

        /// <summary>从 JSON 导入为一个新的 <see cref="ConditionDatabase"/> 实例。</summary>
        public static ConditionDatabase ImportJson(string json, IAssetRefResolver resolver = null)
        {
            var db = ScriptableObject.CreateInstance<ConditionDatabase>();
            ImportJsonInto(json, db, resolver);
            return db;
        }

        /// <summary>把 JSON 导入到既有条件库（先清空其内容）。</summary>
        public static void ImportJsonInto(string json, ConditionDatabase target, IAssetRefResolver resolver = null)
        {
            if (string.IsNullOrEmpty(json) || !target) return;
            FromDto(JsonUtility.FromJson<ConditionDatabaseDto>(json), target, resolver);
        }

        #endregion

        #region 二进制

        /// <summary>导出为紧凑二进制。</summary>
        public static byte[] Export(ConditionDatabase db, IAssetRefResolver resolver = null)
        {
            var dto = ToDto(db, resolver);
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write(Version);
                WriteArray(w, dto.enumTypes, WriteEnumType);
                WriteArray(w, dto.conditionTemplates, WriteTemplate);
                WriteArray(w, dto.conditions, WriteEntry);
            }
            return stream.ToArray();
        }

        /// <summary>从二进制导入为一个新的 <see cref="ConditionDatabase"/> 实例。</summary>
        public static ConditionDatabase Import(byte[] bytes, IAssetRefResolver resolver = null)
        {
            var db = ScriptableObject.CreateInstance<ConditionDatabase>();
            ImportInto(bytes, db, resolver);
            return db;
        }

        /// <summary>把二进制导入到既有条件库（先清空其内容）。魔数不匹配时报错并保持目标不变。</summary>
        public static void ImportInto(byte[] bytes, ConditionDatabase target, IAssetRefResolver resolver = null)
        {
            if (bytes == null || bytes.Length < 8 || !target) return;

            using var stream = new MemoryStream(bytes);
            using var r = new BinaryReader(stream, Encoding.UTF8);

            if (r.ReadInt32() != Magic)
            {
                Debug.LogError("[ConditionConfigSerializer] 魔数不匹配，数据格式无效。");
                return;
            }
            int version = r.ReadInt32();
            if (version > Version)
                Debug.LogWarning($"[ConditionConfigSerializer] 文件版本（{version}）高于当前支持（{Version}），尝试按当前格式解析。");

            var dto = new ConditionDatabaseDto
            {
                version            = version,
                enumTypes          = ReadArray(r, ReadEnumType),
                conditionTemplates = ReadArray(r, ReadTemplate),
                conditions         = ReadArray(r, ReadEntry),
            };
            FromDto(dto, target, resolver);
        }

        private static void WriteTemplate(BinaryWriter w, ConditionTemplateDto t)
        {
            WriteStr(w, t.name);
            WriteFloatArray(w, t.color);
            WriteArray(w, t.attributes, WriteDefinition);
            WriteStr(w, t.defaultExpression != null ? ConditionJson.ToJson(t.defaultExpression, false) : string.Empty);
        }

        private static ConditionTemplateDto ReadTemplate(BinaryReader r)
        {
            var t = new ConditionTemplateDto
            {
                name       = ReadStr(r),
                color      = ReadFloatArray(r),
                attributes = ReadArray(r, ReadDefinition),
            };
            string json = ReadStr(r);
            t.defaultExpression = string.IsNullOrEmpty(json) ? null : ConditionJson.FromJson(json);
            return t;
        }

        private static void WriteEntry(BinaryWriter w, ConditionEntryDto c)
        {
            WriteStr(w, c.id);
            WriteStr(w, c.templateRef);
            WriteValue(w, c.displayText);
            WriteValue(w, c.descriptionText);
            WriteValue(w, c.iconValue);
            WriteEntries(w, c.values);
            WriteStr(w, c.expression != null ? ConditionJson.ToJson(c.expression, false) : string.Empty);
        }

        private static ConditionEntryDto ReadEntry(BinaryReader r)
        {
            var c = new ConditionEntryDto
            {
                id              = ReadStr(r),
                templateRef     = ReadStr(r),
                displayText     = ReadValue(r),
                descriptionText = ReadValue(r),
                iconValue       = ReadValue(r),
                values          = ReadEntries(r),
            };
            string json = ReadStr(r);
            c.expression = string.IsNullOrEmpty(json) ? null : ConditionJson.FromJson(json);
            return c;
        }

        #endregion
    }
}
