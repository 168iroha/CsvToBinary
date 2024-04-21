using BuiltIn;
using System.Xml.Linq;
using System.Xml.XPath;

namespace tests.BultIn
{
    [TestClass]
    public class CounterTests
    {
        [TestMethod]
        public void 定義済みのデフォルトのカウンタの動作()
        {
            long init = 1;
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter",
                    new XElement("count", init)
                )
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            using (var counter = new Counter(stream))
            {
                // 初期値はXMLに記載されたものと等しい
                Assert.AreEqual(init, counter.Count());
                Assert.AreEqual(init + 1, counter.Count());
                Assert.AreEqual(init + 2, counter.Count());
            }

            // XMLが更新されたかの確認
            stream.Position = 0;
            var resultXmlTree = XDocument.Load(stream);
            Assert.AreEqual(init + 3, Int64.Parse(resultXmlTree.XPathSelectElement("/counter/count[not(@name)]")!.Value));
        }

        [TestMethod]
        public void 未定義のデフォルトのカウンタの動作()
        {
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter")
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            using (var counter = new Counter(stream))
            {
                // 初期値はXMLに記載されたものと等しい
                Assert.AreEqual(0, counter.Count());
                Assert.AreEqual(1, counter.Count());
                Assert.AreEqual(2, counter.Count());
            }

            // XMLが更新されたかの確認
            stream.Position = 0;
            var resultXmlTree = XDocument.Load(stream);
            Assert.AreEqual(3, Int64.Parse(resultXmlTree.XPathSelectElement("/counter/count[not(@name)]")!.Value));
        }

        [TestMethod]
        public void 定義済みの名前付きカウンタの動作()
        {
            long init = 1;
            var name = "name";
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter",
                    new XElement("count", new XAttribute("name", name), init)
                )
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            using (var counter = new Counter(stream))
            {
                // 初期値はXMLに記載されたものと等しい
                Assert.AreEqual(init, counter.Count(name));
                Assert.AreEqual(init + 1, counter.Count(name));
                Assert.AreEqual(init + 2, counter.Count(name));
            }

            // XMLが更新されたかの確認
            stream.Position = 0;
            var resultXmlTree = XDocument.Load(stream);
            Assert.AreEqual(init + 3, Int64.Parse(resultXmlTree.XPathSelectElement($"/counter/count[@name='{name}']")!.Value));
        }

        [TestMethod]
        public void 未定義の名前付きカウンタの動作()
        {
            var name = "name";
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter")
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            using (var counter = new Counter(stream))
            {
                // 初期値はXMLに記載されたものと等しい
                Assert.AreEqual(0, counter.Count(name));
                Assert.AreEqual(1, counter.Count(name));
                Assert.AreEqual(2, counter.Count(name));
            }

            // XMLが更新されたかの確認
            stream.Position = 0;
            var resultXmlTree = XDocument.Load(stream);
            Assert.AreEqual(3, Int64.Parse(resultXmlTree.XPathSelectElement($"/counter/count[@name='{name}']")!.Value));
        }

        [TestMethod]
        public void ルートノード名の異常()
        {
            long init = 1;
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter2",
                    new XElement("count", init)
                )
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            try
            {
                using var counter = new Counter(stream);
                Assert.Fail();
            }
            catch (ArgumentException) {}
        }

        [TestMethod]
        public void 複数のカウンタの定義()
        {
            long init1 = 1;
            long init2 = 11;
            long init3 = 111;
            var name2 = "name2";
            var name3 = "name3";
            var name4 = "name4";
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter",
                    new XElement("count", init1),
                    new XElement("count", new XAttribute("name", name2), init2),
                    new XElement("count", new XAttribute("name", name3), init3)
                )
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            using (var counter = new Counter(stream))
            {
                // 初期値はXMLに記載されたものと等しい
                Assert.AreEqual(init1, counter.Count());
                Assert.AreEqual(init2, counter.Count(name2));
                Assert.AreEqual(init3, counter.Count(name3));
                Assert.AreEqual(0, counter.Count(name4));
                Assert.AreEqual(init1 + 1, counter.Count());
                Assert.AreEqual(init2 + 1, counter.Count(name2));
                Assert.AreEqual(init3 + 1, counter.Count(name3));
                Assert.AreEqual(1, counter.Count(name4));
                Assert.AreEqual(init1 + 2, counter.Count());
                Assert.AreEqual(init2 + 2, counter.Count(name2));
                Assert.AreEqual(init3 + 2, counter.Count(name3));
                Assert.AreEqual(2, counter.Count(name4));
            }

            // XMLが更新されたかの確認
            stream.Position = 0;
            var resultXmlTree = XDocument.Load(stream);
            Assert.AreEqual(init1 + 3, Int64.Parse(resultXmlTree.XPathSelectElement("/counter/count[not(@name)]")!.Value));
            Assert.AreEqual(init2 + 3, Int64.Parse(resultXmlTree.XPathSelectElement($"/counter/count[@name='{name2}']")!.Value));
            Assert.AreEqual(init3 + 3, Int64.Parse(resultXmlTree.XPathSelectElement($"/counter/count[@name='{name3}']")!.Value));
            Assert.AreEqual(3, Int64.Parse(resultXmlTree.XPathSelectElement($"/counter/count[@name='{name4}']")!.Value));
        }

        [TestMethod]
        public void デフォルトのカウンタの多重定義()
        {
            long init = 1;
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter",
                    new XElement("count", init),
                    new XElement("count", init)
                )
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            try
            {
                using var counter = new Counter(stream);
                Assert.Fail();
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void 名前付きカウンタの多重定義()
        {
            long init = 1;
            var name = "name";
            // StreamにXMLのセットアップ
            var xmlTree = new XDocument(
                new XElement("counter",
                    new XElement("count", new XAttribute("name", name), init),
                    new XElement("count", new XAttribute("name", name), init)
                )
            );
            using var stream = new MemoryStream();
            xmlTree.Save(stream);
            stream.Position = 0;

            try
            {
                using var counter = new Counter(stream);
                Assert.Fail();
            }
            catch (ArgumentException) { }
        }
    }
}