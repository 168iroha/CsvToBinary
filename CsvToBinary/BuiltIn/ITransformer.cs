namespace BuiltIn
{
    /// <summary>
    /// 文字列の変換を与えるためのインターフェース
    /// </summary>
    public interface ITransformer
    {
        /// <summary>
        /// 文字列を変換する
        /// </summary>
        /// <param name="from">変換元の文字列</param>
        /// <returns>変換結果の文字列</returns>
        public string Transform(string from);
    }
}
