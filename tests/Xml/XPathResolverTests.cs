using System.Xml.Linq;
using System.Xml.XPath;
using Xml;

namespace tests.Xml
{
    [TestClass]
    public class XPathResolverTests
    {
        [TestMethod]
        public void XPathの評価値を文字列として取得()
        {
            var xmlTree = new XElement("items",
                new XElement(
                    "item",
                    new XAttribute("id", 1),
                    "text1"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 2),
                    "text2"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 3),
                    "text3"
                )
            );
            var xPathResolver = new XPathResolver();

            var xpath1 = "//item[@id='2']/text()='text2'";
            var xpath2 = "sum(//item/@id)";
            var xpath3 = "concat(//item[@id='2']/text(), //item[@id='3']/text())";
            var xpath4 = "//item[@id='1']";

            // 3回評価する
            for (int i = 0; i < 3; ++i)
            {
                Assert.AreEqual(xPathResolver.XPathEvaluate(xmlTree, xpath1), true.ToString());
                Assert.AreEqual(xPathResolver.XPathEvaluate(xmlTree, xpath2), 6.ToString());
                Assert.AreEqual(xPathResolver.XPathEvaluate(xmlTree, xpath3), "text2text3");
                Assert.AreEqual(xPathResolver.XPathEvaluate(xmlTree, xpath4), "text1");
            }

            // 空のXPath
            Assert.AreEqual(xPathResolver.XPathEvaluate(xmlTree, ""), "");
        }

        [TestMethod]
        public void XPathの評価値をノードの集合として取得()
        {
            var xmlTree = new XElement("items",
                new XElement(
                    "item",
                    new XAttribute("id", 1),
                    "text1"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 2),
                    "text2"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 3),
                    "text3"
                )
            );
            var xPathResolver = new XPathResolver();

            var xpath1 = "//item";
            var xpath2 = "//item/text()";
            var xpath3 = "//item/@id";

            // 3回評価する
            for (int i = 0; i < 3; ++i)
            {
                // XElementノードの取得
                var itr1 = xPathResolver.XPathSelectNodes(xmlTree, xpath1);
                Assert.AreEqual(itr1.Count, 3);
                Assert.AreEqual(itr1.MoveNext(), true);
                Assert.AreEqual(itr1.Current!.Name, "item");
                Assert.AreEqual(itr1.Current!.HasAttributes, true);
                Assert.AreEqual(itr1.Current!.Value, "text1");
                Assert.AreEqual(itr1.MoveNext(), true);
                Assert.AreEqual(itr1.Current!.Name, "item");
                Assert.AreEqual(itr1.Current!.HasAttributes, true);
                Assert.AreEqual(itr1.Current!.Value, "text2");
                Assert.AreEqual(itr1.MoveNext(), true);
                Assert.AreEqual(itr1.Current!.Name, "item");
                Assert.AreEqual(itr1.Current!.HasAttributes, true);
                Assert.AreEqual(itr1.Current!.Value, "text3");
                Assert.AreEqual(itr1.MoveNext(), false);

                // テキストノードの取得
                var itr2 = xPathResolver.XPathSelectNodes(xmlTree, xpath2);
                Assert.AreEqual(itr2.Count, 3);
                Assert.AreEqual(itr2.MoveNext(), true);
                Assert.AreEqual(itr2.Current!.Name, "");
                Assert.AreEqual(itr2.Current!.HasAttributes, false);
                Assert.AreEqual(itr2.Current!.Value, "text1");
                Assert.AreEqual(itr2.MoveNext(), true);
                Assert.AreEqual(itr2.Current!.Name, "");
                Assert.AreEqual(itr2.Current!.HasAttributes, false);
                Assert.AreEqual(itr2.Current!.Value, "text2");
                Assert.AreEqual(itr2.MoveNext(), true);
                Assert.AreEqual(itr2.Current!.Name, "");
                Assert.AreEqual(itr2.Current!.HasAttributes, false);
                Assert.AreEqual(itr2.Current!.Value, "text3");
                Assert.AreEqual(itr2.MoveNext(), false);

                // 属性ノードの取得
                var itr3 = xPathResolver.XPathSelectNodes(xmlTree, xpath3);
                Assert.AreEqual(itr3.Count, 3);
                Assert.AreEqual(itr3.MoveNext(), true);
                Assert.AreEqual(itr3.Current!.Name, "id");
                Assert.AreEqual(itr3.Current!.HasAttributes, false);
                Assert.AreEqual(itr3.Current!.Value, "1");
                Assert.AreEqual(itr3.MoveNext(), true);
                Assert.AreEqual(itr3.Current!.Name, "id");
                Assert.AreEqual(itr3.Current!.HasAttributes, false);
                Assert.AreEqual(itr3.Current!.Value, "2");
                Assert.AreEqual(itr3.MoveNext(), true);
                Assert.AreEqual(itr3.Current!.Name, "id");
                Assert.AreEqual(itr3.Current!.HasAttributes, false);
                Assert.AreEqual(itr3.Current!.Value, "3");
                Assert.AreEqual(itr3.MoveNext(), false);
            }
        }

        [TestMethod]
        public void XPathの評価値をXElementの集合として取得()
        {
            var xmlTree = new XElement("items",
                new XElement(
                    "item",
                    new XAttribute("id", 1),
                    "text1"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 2),
                    "text2"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 3),
                    "text3"
                )
            );
            var xPathResolver = new XPathResolver();

            var xpath1 = "//item";

            // 3回評価する
            for (int i = 0; i < 3; ++i)
            {
                // XElementノードの取得
                int j = 1;
                foreach (var item in xPathResolver.XPathSelectElements(xmlTree, xpath1))
                {
                    Assert.AreEqual(item.Name, "item");
                    Assert.AreEqual(item.Attribute("id").Value, $"{j}");
                    Assert.AreEqual(item.Value, $"text{j}");
                    ++j;
                }
            }
        }

        [TestMethod]
        public void 異常なXPathの指定()
        {
            var xmlTree = new XElement("item");
            var xPathResolver = new XPathResolver();
            var xpath = "count()";

            try
            {
                xPathResolver.XPathEvaluate(xmlTree, xpath);
                Assert.Fail();
            }
            catch (XPathException) { }

            try
            {
                xPathResolver.XPathSelectNodes(xmlTree, xpath);
                Assert.Fail();
            }
            catch (XPathException) { }

            try
            {
                xPathResolver.XPathSelectElements(xmlTree, xpath).ToArray();
                Assert.Fail();
            }
            catch (XPathException) { }
        }

        [TestMethod]
        public void ノードを返さないXPathの指定()
        {
            var xmlTree = new XElement("item");
            var xPathResolver = new XPathResolver();
            var xpath = "count(//item)";

            try
            {
                xPathResolver.XPathSelectNodes(xmlTree, xpath);
                Assert.Fail();
            }
            catch (XPathException) { }

            try
            {
                xPathResolver.XPathSelectElements(xmlTree, xpath).ToArray();
                Assert.Fail();
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public void ノードを返すがXElementを返さないXPathの指定()
        {
            var xmlTree = new XElement("items",
                new XElement(
                    "item",
                    new XAttribute("id", 1),
                    "text1"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 2),
                    "text2"
                ),
                new XElement(
                    "item",
                    new XAttribute("id", 3),
                    "text3"
                )
            );
            var xPathResolver = new XPathResolver();

            var xpath1 = "//item/text()";
            var xpath2 = "//item/@id";

            try
            {
                xPathResolver.XPathSelectElements(xmlTree, xpath1).ToArray();
                Assert.Fail();
            }
            catch (InvalidOperationException) { }
            try
            {
                xPathResolver.XPathSelectElements(xmlTree, xpath2).ToArray();
                Assert.Fail();
            }
            catch (InvalidOperationException) { }
        }
    }
}
