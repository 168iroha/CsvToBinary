namespace BuiltIn
{
    /// <summary>
    /// 文字列の変換時における異常を示すクラス
    /// </summary>
    public class TransformException : Exception
    {
        /// <summary>
        /// 異常が発生した位置
        /// </summary>
        public int Pos { get; }
        /// <summary>
        /// 該当するテキスト
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="pos">異常が発生した位置</param>
        /// <param name="text">該当するテキスト</param>
        public TransformException(string message, int pos, string text) : base(message)
        {
            this.Pos = pos;
            this.Text = text;
        }
    }
}
