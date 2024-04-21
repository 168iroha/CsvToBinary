using System.Xml.Linq;
using System.Xml.XPath;

namespace Xml
{

    /// <summary>
    /// XMLをバイナリ形式でStreamへ書き込むためのインターフェース
    /// </summary>
    public interface IXmlToBinary
    {
        /// <summary>
        /// XMLのノードの文字列表現を取得
        /// </summary>
        /// <param name="element">文字列表現取得対象のXMLノード</param>
        /// <returns>elementの文字列表現</returns>
        public string GetString(XElement element);

        /// <summary>
        /// XMLのノードを指定したStreamへの書き込み
        /// </summary>
        /// <param name="stream">バイナリの書き込みを行うStream(バッファリングが行われることを前提とする)</param>
        /// <param name="element">書き込み対象のXMLノード</param>
        public void Write(Stream stream, XElement element);
    }
}
