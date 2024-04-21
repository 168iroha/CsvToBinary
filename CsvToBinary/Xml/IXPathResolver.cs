using System.Xml.Linq;
using System.Xml.XPath;

namespace Xml
{

    /// <summary>
    /// XPathを評価するためのインターフェース
    /// </summary>
    public interface IXPathResolver
    {
        /// <summary>
        /// XPathを評価して評価結果のノードの集合を得る
        /// </summary>
        /// <param name="node">XPathの計算の起点となる要素</param>
        /// <param name="xpath">評価するXPath</param>
        /// <returns>評価結果のノードの集合</returns>
        public XPathNodeIterator XPathSelectNodes(XNode node, string xpath);

        /// <summary>
        /// XPathを評価して評価結果の文字列を得る
        /// </summary>
        /// <param name="node">XPathの計算の起点となる要素</param>
        /// <param name="xpath">評価するXPath</param>
        /// <returns>評価結果のテキスト</returns>
        /// <exception cref="InvalidOperationException">これが送信されることはない</exception>
        public string XPathEvaluate(XNode node, string xpath);

        /// <summary>
        /// XPathを評価して評価結果のXElementの集合を得る
        /// </summary>
        /// <param name="node">XPathの計算の起点となる要素</param>
        /// <param name="xpath">評価するXPath</param>
        /// <returns>評価結果のノードの集合</returns>
        public IEnumerable<XElement> XPathSelectElements(XNode node, string xpath);
    }
}
