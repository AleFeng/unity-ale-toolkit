using System;
using System.Collections.Generic;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 层级标签（GAS GameplayTag 范式）：形如 <c>Status.Debuff.Mental</c> 的点分名，父子层级由点分段表达。
    /// 只读结构体，包一条<b>已归一</b>的完整名；<c>default</c> 即 <see cref="None"/>（无效标签）。
    ///
    /// <para><b>序列化形态是 <see cref="string"/>，本结构体永不进入序列化</b>：容器存 <c>List&lt;string&gt;</c>（<see cref="GameplayTagContainer"/>）、
    /// 单标签字段存 <c>string</c>（编辑器以 <c>[GameplayTagField]</c> 标注），对 DTO / 二进制 / Newtonsoft 零迁移。</para>
    ///
    /// <para><b>归一规则（是数据格式，发布后冻结）</b>：整体与逐段 <c>Trim</c>；空段（<c>A..B</c> / <c>.A</c> / <c>A.</c>）非法；
    /// 段内含空白或 <c>/</c> 非法（编辑器菜单以 <c>/</c> 分层）；其余字符（含 CJK、<c>_</c>、<c>-</c>、<c>:</c>）允许。
    /// <b>大小写敏感、序数比较</b>——与 toolkit 全部字符串键（属性 id / 特质 id / 标记）一致；中文段无大小写折叠意义；
    /// 仅大小写不同的手误交给 <see cref="GameplayTagRegistry.Validate"/> 在配置期抓出。</para>
    ///
    /// <para><b>匹配语义</b>：<see cref="MatchesTag"/> 为「自身或后代」——<c>A.B</c> 匹配 <c>A</c>，<c>A</c> 不匹配 <c>A.B</c>；
    /// 纯前缀不算祖先（<c>AB</c> 不匹配 <c>A</c>）。</para>
    /// </summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>, IComparable<GameplayTag>
    {
        /// <summary>层级分隔符。</summary>
        public const char Separator = '.';

        private const string SeparatorString = ".";

        /// <summary>无效标签（<c>default</c>）。任何匹配对它都返回 false。</summary>
        public static readonly GameplayTag None = default;

        // 已归一的完整名；default / 非法输入为 null。
        private readonly string _name;

        /// <summary>由原始字符串构造：按归一规则整理；结构非法时得到 <see cref="None"/>（不抛异常）。</summary>
        public GameplayTag(string raw)
        {
            _name = Normalize(raw);
        }

        // 内部：输入已保证归一（如既有标签的子串），跳过整理。
        private GameplayTag(string normalized, bool _)
        {
            _name = normalized;
        }

        /// <summary>归一后的完整名；无效标签为空串。</summary>
        public string Name => _name ?? string.Empty;

        /// <summary>是否为合法标签。</summary>
        public bool IsValid => !string.IsNullOrEmpty(_name);

        /// <summary>段数（<c>A.B.C</c> = 3）；无效标签为 0。</summary>
        public int Depth
        {
            get
            {
                if (!IsValid) return 0;
                int d = 1;
                for (int i = 0; i < _name.Length; i++)
                    if (_name[i] == Separator) d++;
                return d;
            }
        }

        /// <summary>父标签（去掉末段）；根标签或无效标签返回 <see cref="None"/>。</summary>
        public GameplayTag Parent
        {
            get
            {
                if (!IsValid) return None;
                int i = _name.LastIndexOf(Separator);
                return i < 0 ? None : new GameplayTag(_name.Substring(0, i), true);
            }
        }

        /// <summary>根标签（首段）；无效标签返回 <see cref="None"/>。</summary>
        public GameplayTag Root
        {
            get
            {
                if (!IsValid) return None;
                int i = _name.IndexOf(Separator);
                return i < 0 ? this : new GameplayTag(_name.Substring(0, i), true);
            }
        }

        /// <summary>末段名；无效标签为空串。</summary>
        public string Leaf => IsValid ? _name.Substring(_name.LastIndexOf(Separator) + 1) : string.Empty;

        // ── 匹配 ──────────────────────────────────────────────────────────────────

        /// <summary>本标签是否为 <paramref name="parent"/> 自身或其后代（层级匹配）。任一方无效 → false。</summary>
        public bool MatchesTag(GameplayTag parent)
        {
            if (!IsValid || !parent.IsValid) return false;
            string p = parent._name;
            if (_name.Length < p.Length) return false;
            if (string.CompareOrdinal(_name, 0, p, 0, p.Length) != 0) return false;
            return _name.Length == p.Length || _name[p.Length] == Separator;
        }

        /// <summary>精确相等（无效标签恒 false）。</summary>
        public bool MatchesTagExact(GameplayTag other) => IsValid && Equals(other);

        /// <summary>是否为 <paramref name="ancestor"/> 的严格后代（不含自身）。</summary>
        public bool IsDescendantOf(GameplayTag ancestor) => MatchesTag(ancestor) && _name.Length > ancestor._name.Length;

        /// <summary>是否为 <paramref name="parent"/> 的直接子级。</summary>
        public bool IsDirectChildOf(GameplayTag parent) => IsValid && parent.IsValid && Parent.Equals(parent);

        /// <summary>把祖先由近及远追加进 <paramref name="into"/>（可含自身在最前）。无效标签不追加任何项。</summary>
        public void GetAncestors(List<GameplayTag> into, bool includeSelf = false)
        {
            if (into == null || !IsValid) return;
            if (includeSelf) into.Add(this);
            for (var t = Parent; t.IsValid; t = t.Parent) into.Add(t);
        }

        // ── 解析 / 归一 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 归一：返回规范名，结构非法返回 null。规则见类注释。
        /// 已规范的输入走无分配的快速路径；仅在需要修整段周围空白时才切分重组。
        /// </summary>
        public static string Normalize(string raw)
        {
            if (raw == null) return null;
            string s = raw.Trim();
            if (s.Length == 0) return null;
            if (IsCanonical(s)) return s;

            // 慢路径：逐段修整（允许「A . B」这类段周围空白）；空段 / 段内空白 / '/' 仍非法。
            string[] parts = s.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                string seg = parts[i].Trim();
                if (seg.Length == 0 || !IsSegmentValid(seg)) return null;
                parts[i] = seg;
            }
            return string.Join(SeparatorString, parts);
        }

        /// <summary>原始字符串是否能归一为合法标签。</summary>
        public static bool IsValidName(string raw) => Normalize(raw) != null;

        /// <summary>尝试解析；非法 / 空 → false 且 <paramref name="tag"/> 为 <see cref="None"/>。</summary>
        public static bool TryParse(string raw, out GameplayTag tag)
        {
            tag = new GameplayTag(raw);
            return tag.IsValid;
        }

        /// <summary>解析；非法时抛 <see cref="ArgumentException"/>。</summary>
        public static GameplayTag Parse(string raw)
        {
            var t = new GameplayTag(raw);
            if (!t.IsValid) throw new ArgumentException($"非法的 GameplayTag：'{raw}'", nameof(raw));
            return t;
        }

        private static bool IsCanonical(string s)
        {
            if (s[0] == Separator || s[s.Length - 1] == Separator) return false;
            bool prevSep = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == Separator)
                {
                    if (prevSep) return false;
                    prevSep = true;
                    continue;
                }
                prevSep = false;
                if (!IsSegmentChar(c)) return false;
            }
            return true;
        }

        private static bool IsSegmentValid(string seg)
        {
            for (int i = 0; i < seg.Length; i++)
                if (!IsSegmentChar(seg[i])) return false;
            return true;
        }

        private static bool IsSegmentChar(char c) => !char.IsWhiteSpace(c) && c != '/';

        // ── 相等 / 比较 ────────────────────────────────────────────────────────────

        public bool Equals(GameplayTag other) => string.Equals(_name, other._name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => _name != null ? StringComparer.Ordinal.GetHashCode(_name) : 0;

        public int CompareTo(GameplayTag other) => string.CompareOrdinal(Name, other.Name);

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);

        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);

        public override string ToString() => Name;
    }
}
