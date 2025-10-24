using System.Xml.Linq;
using System.Xml.XPath;

namespace CsvToBinary.Xml
{

    /// <summary>
    /// XPathのコンパイル結果をキャッシュしながら評価するためのクラス
    /// </summary>
    public class XPathResolver : IXPathResolver
    {
        private readonly Dictionary<string, XPathExpression> xPathCache = [];

        /// <summary>
        /// XPathを評価して評価結果の文字列を得る
        /// </summary>
        /// <param name="node">XPathの計算の起点となる要素</param>
        /// <param name="xpath">評価するXPath</param>
        /// <returns>評価結果のテキスト</returns>
        /// <exception cref="InvalidOperationException">これが送信されることはない</exception>
        public string XPathEvaluate(XNode node, string xpath)
        {
            if (string.IsNullOrEmpty(xpath))
            {
                return "";
            }

            // XPathの評価をキャッシュするためにXPathNavigatorを利用する(queryはDocumentを問わず使用可能)
            //var result = node.XPathEvaluate(xpath);
            var navigator = node.CreateNavigator();
            if (!this.xPathCache.TryGetValue(xpath, out XPathExpression? query))
            {
                query = navigator.Compile(xpath);
                this.xPathCache.Add(xpath, query);
            }
            var result = navigator.Evaluate(query);
            return result switch
            {
                bool b => b.ToString(),
                double d => d.ToString(),
                string s => s,
                // 最初のノードの値の文字列を返す
                XPathNodeIterator x => x.MoveNext() ? x.Current?.Value ?? "" : "",
                // ここに来ることはない
                _ => throw new InvalidOperationException("呼び出されることはありません")
            };
        }

        /// <summary>
        /// XPathを評価して評価結果のノードの集合を得る
        /// </summary>
        /// <param name="node">XPathの計算の起点となる要素</param>
        /// <param name="xpath">評価するXPath</param>
        /// <returns>評価結果のノードの集合</returns>
        public XPathNodeIterator XPathSelectNodes(XNode node, string xpath)
        {
            var navigator = node.CreateNavigator();
            if (!this.xPathCache.TryGetValue(xpath, out XPathExpression? query))
            {
                query = navigator.Compile(xpath);
                this.xPathCache.Add(xpath, query);
            }
            return navigator.Select(query);
        }

        /// <summary>
        /// XPathを評価して評価結果のXElementの集合を得る
        /// </summary>
        /// <param name="node">XPathの計算の起点となる要素</param>
        /// <param name="xpath">評価するXPath</param>
        /// <returns>評価結果のノードの集合</returns>
        public IEnumerable<XElement> XPathSelectElements(XNode node, string xpath)
        {
            var navigator = node.CreateNavigator();
            if (!this.xPathCache.TryGetValue(xpath, out XPathExpression? query))
            {
                query = navigator.Compile(xpath);
                this.xPathCache.Add(xpath, query);
            }
            var result = navigator.Evaluate(query);

            // XPathの実行結果をループで回して返す
            var itr = result as XPathNodeIterator;
            if (itr is not null)
            {
                foreach (XPathNavigator nav in itr)
                {
                    var r = nav.UnderlyingObject;
                    if (r is not XElement)
                    {
                        throw new InvalidOperationException("XPathで選択した対象にXElement以外のノードが含まれています");
                    }
                    yield return (XElement)r;
                }
            }
            else
            {
                throw new InvalidOperationException("XPathの実行結果にノードが含まれていません");
            }
        }
    }
}
