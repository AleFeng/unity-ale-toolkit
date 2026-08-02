using NUnit.Framework;
using Ale.Toolkit.Runtime;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// TextValue 门槛：默认空、fallback 解析、本地化引用读写、Clone 独立。
    /// 本地化解析分支依赖运行时本地化设置（表/条目），不在纯单测覆盖；此处仅验证「无本地化引用时 ResolveText 回退 fallback」。
    /// </summary>
    public class TextValueTests
    {
        [Test]
        public void Default_IsEmpty_And_ResolvesToEmpty()
        {
            var t = new TextValue();
            Assert.IsTrue(t.IsEmpty);
            Assert.AreEqual(string.Empty, t.ResolveText());
            Assert.AreEqual(string.Empty, t.Fallback);
        }

        [Test]
        public void Fallback_Ctor_And_Resolve()
        {
            var t = new TextValue("列表");
            Assert.IsFalse(t.IsEmpty);
            Assert.AreEqual("列表", t.Fallback);
            Assert.AreEqual("列表", t.ResolveText());   // 无本地化引用 → 回退纯文本
        }

        [Test]
        public void Fallback_Setter_NullBecomesEmpty()
        {
            var t = new TextValue("x");
            t.Fallback = null;
            Assert.AreEqual(string.Empty, t.Fallback);
            Assert.IsTrue(t.IsEmpty);
        }

        [Test]
        public void LocalizedRef_Roundtrip()
        {
            var t = new TextValue();
            t.SetLocalizedRef("UI表", "list_key");
            var (table, entry) = t.GetLocalizedRef();
            Assert.AreEqual("UI表", table);
            Assert.AreEqual("list_key", entry);
            Assert.IsFalse(t.IsEmpty);   // 有本地化引用即非空
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var src = new TextValue("网格");
            src.SetLocalizedRef("T", "E");
            var dst = src.Clone();

            Assert.AreEqual("网格", dst.Fallback);
            Assert.AreEqual(("T", "E"), dst.GetLocalizedRef());

            dst.Fallback = "改了";
            dst.SetLocalizedRef("T2", "E2");
            Assert.AreEqual("网格", src.Fallback);                 // 源不受克隆改动影响
            Assert.AreEqual(("T", "E"), src.GetLocalizedRef());
        }
    }
}
