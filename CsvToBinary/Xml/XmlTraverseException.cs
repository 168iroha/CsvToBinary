using System.Xml.Linq;

namespace CsvToBinary.Xml
{
    /// <summary>
    /// XMLのトラバーサル時の論理的なエラーに関する例外クラス
    /// </summary>
    public class XmlTraverseException : Exception
    {
        /// <summary>
        /// 異常を検知したXMLノード
        /// </summary>
        public XElement? Element { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="element">異常が発生した要素</param>
        public XmlTraverseException(string message, XElement? element) : base(message)
        {
            this.Element = element;
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="element">異常が発生した要素</param>
        /// <param name="inner">内部例外</param>
        public XmlTraverseException(string message, XElement? element, Exception? inner) : base(message, inner)
        {
            this.Element = element;
        }
    }
}
