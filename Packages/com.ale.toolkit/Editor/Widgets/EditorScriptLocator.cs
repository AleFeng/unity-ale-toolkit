using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 「类型 → 源码脚本」定位器：把一个 <see cref="Type"/> 解析为工程里的 <see cref="MonoScript"/> 资产，用于编辑器面板
    /// 从「已发现的实现」跳回源码（打开 IDE / 在 Project 窗口高亮）。
    ///
    /// <para><b>为什么不能只靠 <c>MonoScript.GetClass()</c></b>：它只对 <see cref="MonoBehaviour"/> / <see cref="ScriptableObject"/>
    /// 派生类返回类型，普通 C# 类（如效果执行器、条件判定器）一律返回 null。因此本类按
    /// ① <c>GetClass()</c> 命中 → ② 文件名与类型名相同<b>且</b>脚本文本里确有该命名空间与声明 → ③ 仅文件名相同
    /// 的顺序回退。脚本文本一律取自 <see cref="MonoScript.text"/> 而非 <see cref="File"/>——经 <c>file:</c> 依赖挂载的包，
    /// <c>Packages/xxx/…</c> 是虚拟路径，磁盘上并不存在。</para>
    ///
    /// <para><b>限制</b>：来自预编译 DLL 的类型没有 <see cref="MonoScript"/>（返回 null）；嵌套类型按其外层文件名查找可能落空。</para>
    /// </summary>
    public static class EditorScriptLocator
    {
        private static readonly Dictionary<Type, string> _paths = new Dictionary<Type, string>();

        /// <summary>清空缓存（脚本重编译 / 资产移动后调用）。</summary>
        public static void ClearCache() => _paths.Clear();

        /// <summary>类型所在脚本的资产路径；未找到（或来自预编译 DLL）返回 null。结果带缓存。</summary>
        public static string PathOf(Type type)
        {
            if (type == null) return null;
            if (_paths.TryGetValue(type, out var cached)) return cached;
            string path = Resolve(type);
            _paths[type] = path;
            return path;
        }

        /// <summary>类型所在的 <see cref="MonoScript"/>；未找到返回 null。</summary>
        public static MonoScript Find(Type type)
        {
            string path = PathOf(type);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        }

        /// <summary>在外部 IDE 中打开脚本并定位到类声明行；未找到返回 false。</summary>
        public static bool Open(Type type)
        {
            var ms = Find(type);
            if (!ms) return false;
            AssetDatabase.OpenAsset(ms, DeclarationLine(ms, type));
            return true;
        }

        /// <summary>在 Project 窗口中选中并高亮脚本；未找到返回 false。</summary>
        public static bool Ping(Type type)
        {
            var ms = Find(type);
            if (!ms) return false;
            Selection.activeObject = ms;
            EditorGUIUtility.PingObject(ms);
            return true;
        }

        // ── 内部 ──────────────────────────────────────────────────────────────────

        private static string Resolve(Type type)
        {
            string simpleName = SimpleName(type);
            if (string.IsNullOrEmpty(simpleName)) return null;

            string exact = null;   // 文件名相同 + 文本确认
            string loose = null;   // 仅文件名相同

            foreach (var guid in AssetDatabase.FindAssets(simpleName + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (!ms) continue;

                if (ms.GetClass() == type) return path;   // ① 最可靠

                if (!string.Equals(Path.GetFileNameWithoutExtension(path), simpleName, StringComparison.Ordinal)) continue;
                if (exact == null && Declares(ms, type, simpleName)) exact = path;   // ②
                else loose ??= path;                                                  // ③
            }
            return exact ?? loose;
        }

        /// <summary>泛型类型名去掉反引号元数（<c>Foo`1</c> → <c>Foo</c>）。</summary>
        private static string SimpleName(Type type)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            return tick > 0 ? name.Substring(0, tick) : name;
        }

        /// <summary>脚本文本里是否确有该类型的命名空间与声明。</summary>
        private static bool Declares(MonoScript ms, Type type, string simpleName)
        {
            string text = ms.text;
            if (string.IsNullOrEmpty(text)) return false;

            string ns = type.Namespace;
            if (!string.IsNullOrEmpty(ns) && text.IndexOf("namespace " + ns, StringComparison.Ordinal) < 0)
                return false;

            return FindDeclarationIndex(text, simpleName) >= 0;
        }

        /// <summary>类声明所在行（1 起）；找不到返回 0（Unity 会从文件开头打开）。</summary>
        private static int DeclarationLine(MonoScript ms, Type type)
        {
            string text = ms.text;
            if (string.IsNullOrEmpty(text)) return 0;

            int at = FindDeclarationIndex(text, SimpleName(type));
            if (at < 0) return 0;

            int line = 1;
            for (int i = 0; i < at; i++) if (text[i] == '\n') line++;
            return line;
        }

        /// <summary><c>class/struct/interface/record Name</c> 的出现位置；未找到返回 -1。</summary>
        private static int FindDeclarationIndex(string text, string simpleName)
        {
            string[] keywords = { "class ", "struct ", "interface ", "record " };
            int best = -1;
            foreach (var kw in keywords)
            {
                int from = 0;
                while (true)
                {
                    int at = text.IndexOf(kw + simpleName, from, StringComparison.Ordinal);
                    if (at < 0) break;

                    // 名字后必须是非标识符字符（避免 FooBar 命中 Foo）
                    int after = at + kw.Length + simpleName.Length;
                    char next = after < text.Length ? text[after] : ' ';
                    if (!char.IsLetterOrDigit(next) && next != '_')
                    {
                        if (best < 0 || at < best) best = at;
                        break;
                    }
                    from = at + 1;
                }
            }
            return best;
        }
    }
}
