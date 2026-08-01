using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 单个效果参数值（<c>AttributeValue</c> 的极简化：仅标量 5 型 + 数组）。
    /// 用「扁平后备列表」承载，无多态、无 Dictionary —— 既可被 Unity 原生序列化（+Undo），
    /// 又是纯 POCO 可被 Newtonsoft JSON 往返，核心程序集零 UnityEngine 依赖。
    /// 与 <c>Ale.Condition.ConditionParam</c> 同构、各自平行复制（不共享，隔离 Condition 系统）。
    /// <para>后备分桶：Int / Bool(0/1) / Enum → <see cref="ints"/>；Float → <see cref="floats"/>；String → <see cref="strings"/>。</para>
    /// </summary>
    [Serializable]
    public class EffectParam
    {
        /// <summary>参数标识（与执行器 <see cref="EffectParamDef.id"/> 对应）。</summary>
        public string id;

        /// <summary>值类型。</summary>
        public EffectParamType type;

        /// <summary>是否数组。</summary>
        public bool isArray;

        /// <summary>当 <see cref="type"/> 为 Enum 时引用的枚举类型名（供编辑器下拉；核心不解释）。</summary>
        public string enumTypeRef;

        /// <summary>整数后备（Int / Bool 以 0/1 存 / Enum）。</summary>
        public List<long> ints = new List<long>();

        /// <summary>浮点后备（Float）。</summary>
        public List<double> floats = new List<double>();

        /// <summary>字符串后备（String）。</summary>
        public List<string> strings = new List<string>();

        public EffectParam() { }

        public EffectParam(string id, EffectParamType type, bool isArray = false, string enumTypeRef = null)
        {
            this.id          = id;
            this.type        = type;
            this.isArray     = isArray;
            this.enumTypeRef = enumTypeRef;
        }

        /// <summary>逻辑元素数（按 <see cref="type"/> 落在对应后备列表的长度）。</summary>
        public int Count
        {
            get
            {
                switch (type)
                {
                    case EffectParamType.String: return strings.Count;
                    case EffectParamType.Float:  return floats.Count;
                    default:                     return ints.Count; // Int / Bool / Enum
                }
            }
        }

        // ── 读取（越界返回默认值）──────────────────────────────────────────────────
        public long   GetInt(int index = 0)    => index >= 0 && index < ints.Count    ? ints[index]    : 0L;
        public bool   GetBool(int index = 0)   => GetInt(index) != 0L;
        public long   GetEnum(int index = 0)   => GetInt(index);
        public double GetFloat(int index = 0)  => index >= 0 && index < floats.Count  ? floats[index]  : 0d;
        public string GetString(int index = 0) => index >= 0 && index < strings.Count ? strings[index] : null;

        // ── 写入（不足处补默认值）──────────────────────────────────────────────────
        public void SetInt(long v, int index = 0)     { Ensure(ints, index + 1, 0L);   ints[index]    = v; }
        public void SetBool(bool v, int index = 0)    => SetInt(v ? 1L : 0L, index);
        public void SetEnum(long v, int index = 0)    => SetInt(v, index);
        public void SetFloat(double v, int index = 0) { Ensure(floats, index + 1, 0d); floats[index]  = v; }
        public void SetString(string v, int index = 0){ Ensure(strings, index + 1, null); strings[index] = v; }

        private static void Ensure<T>(List<T> list, int count, T fill)
        {
            while (list.Count < count) list.Add(fill);
        }

        public EffectParam Clone()
        {
            return new EffectParam(id, type, isArray, enumTypeRef)
            {
                ints    = new List<long>(ints),
                floats  = new List<double>(floats),
                strings = new List<string>(strings),
            };
        }
    }
}
