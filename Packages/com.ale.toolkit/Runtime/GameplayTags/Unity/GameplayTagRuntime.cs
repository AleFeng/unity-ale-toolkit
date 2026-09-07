using System.Collections.Generic;
using UnityEngine;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 运行时桥：游戏启动时清空并重新填充 <see cref="GameplayTagRegistry.Default"/>——载入全部 <c>Resources</c> 目录下的
    /// <see cref="GameplayTagTable"/>（零配置默认）；另提供显式登记入口给有自己数据管线的宿主（如数据库注册时并入其自定义标签）。
    /// 注册表是<b>咨询性</b>的：没有标签表也不影响运行时匹配。
    /// </summary>
    public static class GameplayTagRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var reg = GameplayTagRegistry.Default;
            reg.Clear();
            foreach (var table in Resources.LoadAll<GameplayTagTable>(string.Empty))
                if (table) table.RegisterInto(reg);
        }

        /// <summary>把一份标签表登记进默认注册表，返回新增数。</summary>
        public static int Register(GameplayTagTable table) => table ? table.RegisterInto(GameplayTagRegistry.Default) : 0;

        /// <summary>登记一条标签（自动补祖先），返回是否新增。</summary>
        public static bool Register(string name, string comment = null) => GameplayTagRegistry.Default.Register(name, comment);

        /// <summary>登记一组定义条目（宿主数据库的自定义标签），返回新增数。</summary>
        public static int Register(IEnumerable<GameplayTagDefinition> definitions) => GameplayTagRegistry.Default.Register(definitions);
    }
}
