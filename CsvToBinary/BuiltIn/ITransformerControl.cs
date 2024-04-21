namespace BuiltIn
{
    /// <summary>
    /// 複数のパターンの文字列の変換の管理のためのインターフェース
    /// </summary>
    public interface ITransformerControl
    {
        /// <summary>
        /// 文字列を変換をするための変換器の取得
        /// </summary>
        /// <param name="name">変換器の名称</param>
        /// <returns>変換結果の文字列</returns>
        public ITransformer Get(string name);
    }
}
