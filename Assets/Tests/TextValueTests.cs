using NUnit.Framework;
using Ale.Toolkit.Runtime;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// TextValue 门槛：默认空、fallback 解析与读写、Clone 独立。
    /// 本地化引用由内嵌的 Unity LocalizedString 承载（原生序列化 / 绘制器），其解析依赖运行时本地化设置，
    /// 不在纯单测覆盖；此处验证「无本地化引用时 ResolveText 回退 fallback」。
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
        public void Clone_CopiesFallback_Independent()
        {
            var src = new TextValue("网格");
            var dst = src.Clone();
            Assert.AreEqual("网格", dst.Fallback);

            dst.Fallback = "改了";
            Assert.AreEqual("网格", src.Fallback);   // 源不受克隆改动影响
        }
    }
}
