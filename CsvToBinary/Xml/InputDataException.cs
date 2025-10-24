using System.Xml.Linq;

namespace CsvToBinary.Xml
{
    /// <summary>
    /// 入力データに関する例外クラス
    /// </summary>
    public class InputDataException : XmlTraverseException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="element">異常が発生した要素</param>
        public InputDataException(string message, XElement? element) : base(message, element) { }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="element">異常が発生した要素</param>
        /// <param name="inner">内部例外</param>
        public InputDataException(string message, XElement? element, Exception inner) : base(message, element, inner) { }
    }
}
