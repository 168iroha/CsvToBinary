using System.Xml.Linq;

namespace Xml
{
    /// <summary>
    /// 無限ループに関する例外クラス
    /// </summary>
    public class InfiniteLoopException : XmlFormatException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="element">異常が発生した要素</param>
        public InfiniteLoopException(string message, XElement? element) : base(message, element) { }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="element">異常が発生した要素</param>
        /// <param name="inner">内部例外</param>
        public InfiniteLoopException(string message, XElement? element, Exception inner) : base(message, element, inner) { }
    }
}
