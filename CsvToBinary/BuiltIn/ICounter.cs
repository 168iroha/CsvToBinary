namespace BuiltIn
{
    /// <summary>
    /// 連番を与えるためのインターフェース
    /// </summary>
    public interface ICounter
    {
        /// <summary>
        /// カウントアップを行い、現在のカウントを取得する
        /// </summary>
        /// <param name="name">カウンタの名称</param>
        /// <returns>現在のカウント</returns>
        public long Count(string? name);
    }
}
