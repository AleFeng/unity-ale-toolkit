namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 缓动函数类型（最小集，供 <see cref="ToolkitTween"/> 使用；按需增量扩展）。
    /// </summary>
    public enum EToolkitEase
    {
        /// <summary>线性（无缓动）。</summary>
        Linear,
        /// <summary>二次缓入（慢→快）。</summary>
        InQuad,
        /// <summary>二次缓出（快→慢）。</summary>
        OutQuad,
        /// <summary>二次缓入缓出。</summary>
        InOutQuad,
    }

    /// <summary>
    /// 缓动求值：把线性进度 <c>t∈[0,1]</c> 映射为缓动后的插值系数。
    /// </summary>
    public static class ToolkitEase
    {
        /// <summary>按 <paramref name="ease"/> 对线性进度 <paramref name="t"/>（自动 Clamp01）求缓动值。</summary>
        public static float Evaluate(EToolkitEase ease, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            switch (ease)
            {
                case EToolkitEase.InQuad:    return t * t;
                case EToolkitEase.OutQuad:   return t * (2f - t);
                case EToolkitEase.InOutQuad: return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
                default:                     return t; // Linear
            }
        }
    }
}
