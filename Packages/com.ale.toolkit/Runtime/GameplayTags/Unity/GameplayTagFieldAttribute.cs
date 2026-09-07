using System;
using UnityEngine;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标注在 <c>string</c> 字段上，让 Inspector 以「标签字段」绘制：文本框 + 目录树下拉，非法名红底、未登记黄底。
    /// 用法：<c>[GameplayTagField] public string cueTag;</c>。序列化形态仍是普通字符串。
    /// （命名带 <c>Field</c> 以避免与类型 <see cref="GameplayTag"/> 在 <c>[GameplayTag]</c> 写法下产生二义性。）
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class GameplayTagFieldAttribute : PropertyAttribute
    {
    }
}
