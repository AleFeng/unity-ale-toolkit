using System;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签定义条目（名称 + 说明）：标签表资产与宿主数据库里「声明本项目有哪些标签」的载体，
    /// 由 <see cref="GameplayTagRegistry.Register(string, string)"/> 登记。纯数据，<c>[Serializable]</c>。
    /// </summary>
    [Serializable]
    public class GameplayTagDefinition
    {
        /// <summary>标签名（点分层级，如 <c>Status.Debuff.Mental</c>）。</summary>
        public string name;

        /// <summary>说明（编辑器提示用，可空）。</summary>
        public string comment;

        public GameplayTagDefinition()
        {
        }

        public GameplayTagDefinition(string name, string comment = null)
        {
            this.name    = name;
            this.comment = comment;
        }

        public GameplayTagDefinition Clone() => new GameplayTagDefinition(name, comment);
    }
}
