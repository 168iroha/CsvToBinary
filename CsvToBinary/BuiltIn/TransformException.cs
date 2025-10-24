namespace CsvToBinary.BuiltIn
{
    /// <summary>
    /// 文字列の変換時における異常を示すクラス
    /// </summary>
    /// <param name="message">例外のメッセージ</param>
    /// <param name="pos">異常が発生した位置</param>
    /// <param name="text">該当するテキスト</param>
    public class TransformException(string message, int pos, string text) : Exception(message)
    {
        /// <summary>
        /// 異常が発生した位置
        /// </summary>
        public int Pos { get; } = pos;
        /// <summary>
        /// 該当するテキスト
        /// </summary>
        public string Text { get; } = text;
    }
}
