#if ATK_LOCALIZATION
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ale.Toolkit.Runtime;
using Object = UnityEngine.Object;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 反射遍历任意对象图，收集其中全部 <see cref="EFieldType.Text"/> 型 <see cref="AttributeValue"/>，
    /// 为每处产出带**结构化反射路径**（字段名 / 索引，如 <c>MyDatabase-Skills-3-displayText</c>）的
    /// <see cref="TextFieldRef"/>，供通用本地化工具生成唯一 Key。
    ///
    /// <para>与 <see cref="AttributeValueWalker"/> 同构（同一套下钻规则、遇 <see cref="Object"/> 即止），
    /// 差别仅在收集谓词（Text 型而非对象类）与产出（带路径 Key，经 <see cref="TextFieldCollector"/> 去重）。
    /// 通用工具无领域知识，Key 只能是结构化反射路径、不含业务语义——需要语义 Key 的宿主自建专用收集器。</para>
    /// </summary>
    public static class TextFieldWalker
    {
        /// <summary>从 <paramref name="root"/>（通常是数据库资产）的序列化字段起步下钻，收集全部 Text 型属性值。</summary>
        public static List<TextFieldRef> Collect(object root)
        {
            var collector = new TextFieldCollector();
            var visited   = new HashSet<object>(RefComparer.Instance);
            if (root != null)
            {
                var    uObj     = root as Object;
                string rootName = uObj ? uObj.name : null;
                string baseKey  = string.IsNullOrEmpty(rootName) ? root.GetType().Name : rootName;
                foreach (var f in root.GetType().GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    Walk(f.GetValue(root), Combine(baseKey, f.Name), collector, visited);
            }
            return collector.Result;
        }

        private static void Walk(object node, string path, TextFieldCollector collector, HashSet<object> visited)
        {
            if (node == null) return;

            // 命中属性值：收集 Text 型的（标量 1 个 / 数组逐元素由收集器处理），停止下钻
            if (node is AttributeValue av)
            {
                if (av.Type == EFieldType.Text) collector.AddText(av, path);
                return;
            }

            // 外部资源引用 / 值类型（结构体，不含 AttributeValue）/ 字符串：不下钻
            if (node is Object) return;
            var type = node.GetType();
            if (type.IsValueType || node is string) return;
            if (!visited.Add(node)) return;

            // 集合逐元素下钻（路径追加索引）
            if (node is System.Collections.IEnumerable en)
            {
                int i = 0;
                foreach (var e in en)
                {
                    Walk(e, Combine(path, i.ToString()), collector, visited);
                    i++;
                }
                return;
            }

            // 普通 [Serializable] 数据类：逐字段下钻（路径追加字段名）
            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var ft = f.FieldType;
                if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string)) continue;
                if (typeof(Object).IsAssignableFrom(ft)) continue;   // 跳过外部资源引用字段
                Walk(f.GetValue(node), Combine(path, f.Name), collector, visited);
            }
        }

        private static string Combine(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "-" + b;

        /// <summary>引用相等比较器（避免值类型 / 重写 Equals 干扰访问过标记）。</summary>
        private sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new RefComparer();
            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
            int IEqualityComparer<object>.GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}
#endif
