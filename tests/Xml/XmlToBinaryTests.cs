using CsvToBinary.BuiltIn;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using CsvToBinary.Xml;

namespace tests.Xml
{
    static class Extensions
    {
        /// <summary>
        /// Streamの全てのバイナリデータを得るための拡張メソッド
        /// </summary>
        /// <param name="stream">バイナリデータの取得元のStream</param>
        /// <returns>Streamの全てのバイナリデータ</returns>
        public static byte[] ReadAll(this Stream stream)
        {
            long pos = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            var byteArray = new byte[stream.Length];
            stream.Read(byteArray, 0, byteArray.Length);
            stream.Seek(pos, SeekOrigin.Begin);

            return byteArray;
        }
    }

    /// <summary>
    /// ITransformerについてのスタブ
    /// </summary>
    public class StubTransformer : ITransformer
    {
        public string Transform(string from)
        {
            // 少なくともfromとは異なる文字列を返す
            return string.IsNullOrEmpty(from) ? "empty" : from + from;
        }
    }
    public class StubTransformerControl : ITransformerControl
    {
        /// <summary>
        /// 最後に参照された変換器の名称
        /// </summary>
        public string? Reference { get; private set; } = null;

        public ITransformer Get(string name)
        {
            Reference = name;
            return new StubTransformer();
        }
    }

    /// <summary>
    /// ICounterについてのスタブ
    /// </summary>
    public class StubCounter : ICounter
    {
        private long count = 0;
        /// <summary>
        /// 最後に参照されたカウンタの名称
        /// </summary>
        public string? Reference { get; private set; } = null;

        public long Count(string? name)
        {
            Reference = name;
            return count++;
        }
    }

    public class StubXPathResolver : IXPathResolver
    {
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
            return node.XPathEvaluate(xpath) switch
            {
                bool b => b.ToString(),
                double d => d.ToString(),
                string s => s,
                IEnumerable => throw new NotImplementedException("未実装"),
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
            var query = navigator.Compile(xpath);
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
            return node.XPathSelectElements(xpath);
        }
    }

    [TestClass]
    public class XmlToBinaryTests
    {
        [TestMethod]
        public void テキスト要素の出力()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree);
            Assert.AreEqual(xmlTree.Attribute("result")?.Value, item);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(item));
        }

        [TestMethod]
        public void デフォルトのテキスト要素の出力()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("default-value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree);
            Assert.AreEqual(xmlTree.Attribute("result")?.Value, item);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(item));
        }

        [TestMethod]
        public void デフォルトを利用しないテキスト要素の出力()
        {
            var item1 = "あいうえお";
            var item2 = "かきくけこ";

            var xmlTree1 = new XElement("item",
                new XElement("value", item1),
                new XElement("default-value", item2)
            );
            // xmlTree1とは逆順に記載
            var xmlTree2 = new XElement("item",
                new XElement("default-value", item2),
                new XElement("value", item1)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree1);
            Assert.AreEqual(xmlTree1.Attribute("result")?.Value, item1);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(item1));
            stream.SetLength(0);
            xmlToBinary.Write(stream, xmlTree2);
            Assert.AreEqual(xmlTree2.Attribute("result")?.Value, item1);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(item1));
        }

        [TestMethod]
        public void 文字コード変換付きのテキスト要素の出力()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );
            // UTF-16でバイナリを出力する
            xmlTree.SetAttributeValue("encoding", "utf-16");

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.Unicode.GetBytes(item));
        }

        [TestMethod]
        public void 幅を指定したテキスト要素の出力()
        {
            var item = "あいうえお";
            var targets = Encoding.UTF8.GetBytes(item);
            var length1 = targets.Length * 2;
            var length2 = targets.Length / 2;

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // 十分に大きな幅を指定して出力
            xmlTree.SetAttributeValue("bytes", length1);
            xmlToBinary.Write(stream, xmlTree);
            var bytes1 = stream.ReadAll();
            Assert.AreEqual(xmlTree.Attribute("result")?.Value, item);
            Assert.AreEqual(bytes1.Length, length1);
            CollectionAssert.AreEqual(bytes1[..targets.Length], targets);
            // 余りはnull埋め
            CollectionAssert.AreEqual(bytes1[targets.Length..], new byte[length1 - targets.Length]);

            stream.SetLength(0);
            // 不足した幅を指定して出力
            xmlTree.SetAttributeValue("bytes", length2);
            xmlToBinary.Write(stream, xmlTree);
            var bytes2 = stream.ReadAll();
            Assert.AreEqual(xmlTree.Attribute("result")?.Value, item);
            Assert.AreEqual(bytes2.Length, length2);
            CollectionAssert.AreEqual(bytes2[..length2], targets[..length2]);
        }

        [TestMethod]
        public void 数値以外の幅を指定したテキスト要素の出力()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );
            xmlTree.SetAttributeValue("bytes", "abc");

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            try
            {
                xmlToBinary.Write(stream, xmlTree);
                Assert.Fail();
            }
            catch (FormatException)
            {
                // 書き込み異常が生じたときにStreamへの書き込みが行われないこと
                Assert.AreEqual(stream.Length, 0);
            }
        }

        [TestMethod]
        public void 何も出力されない幅を指定したテキスト要素の出力()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // 幅が0
            xmlTree.SetAttributeValue("bytes", "0");
            xmlToBinary.Write(stream, xmlTree);
            Assert.AreEqual(stream.Length, 0);

            // 幅が負
            xmlTree.SetAttributeValue("bytes", "-1");
            xmlToBinary.Write(stream, xmlTree);
            Assert.AreEqual(stream.Length, 0);
        }

        [TestMethod]
        public void 異常な幅を指定したテキスト要素の出力()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // 最大値を超える幅を指定
            xmlTree.SetAttributeValue("bytes", "2147483648");
            try
            {
                xmlToBinary.Write(stream, xmlTree);
                Assert.Fail();
            }
            catch (OverflowException)
            {
                // 書き込み異常が生じたときにStreamへの書き込みが行われないこと
                Assert.AreEqual(stream.Length, 0);
            }
        }

        [TestMethod]
        public void XPathによる幅を指定したテキスト要素の出力()
        {
            var item = "あいうえお";
            var targets = Encoding.UTF8.GetBytes(item);
            var length = targets.Length * 2;
            var name = "項目長";

            var xmlSubTree = new XElement("item",
                new XElement("value", item)
            );
            _ = new XElement("items",
                // XPathで参照するための兄弟ノードの定義
                new XElement("item",
                    new XAttribute("name", name),
                    new XElement("value", length)
                ),
                xmlSubTree
            );
            xmlSubTree.SetAttributeValue("xbytes", $"sum(../item[@name='{name}']/value)");

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            xmlToBinary.Write(stream, xmlSubTree);
            var bytes = stream.ReadAll();
            Assert.AreEqual(bytes.Length, length);
            CollectionAssert.AreEqual(bytes[..targets.Length], targets);
        }

        [TestMethod]
        public void パディングを利用したテキスト要素の出力()
        {
            var item = "あいうえお";
            var padding = "123";
            var length = 100;

            var xmlTree = new XElement("item",
                new XElement("value", item),
                new XAttribute("padding", padding)
            );
            xmlTree.SetAttributeValue("bytes", length);

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree);
            var bytes = stream.ReadAll();
            var targets = Encoding.UTF8.GetBytes(item);
            // パディングではないデータ本体の検査
            Assert.AreEqual(bytes.Length, length);
            CollectionAssert.AreEqual(bytes[..targets.Length], targets);
            // パディングの検査
            var paddingBytes = Encoding.UTF8.GetBytes(padding);
            int i = targets.Length;
            for (; i < length - paddingBytes.Length; i += paddingBytes.Length)
            {
                // パディングが書き込み可能な数だけ繰り返し書き込まれること
                CollectionAssert.AreEqual(bytes[i..(i + paddingBytes.Length)], paddingBytes);
            }
            if (i < length)
            {
                // 余ったパディングは切り詰められること
                CollectionAssert.AreEqual(bytes[i..], paddingBytes[..(length - i)]);
            }
            else
            {
                // 上記が呼び出されないテストはNG
                Assert.Fail();
            }
        }

        [TestMethod]
        public void 明示的な右からのパディングを指定したテキスト要素の出力()
        {
            var item = "あいうえお";
            // 簡単のために1バイト文字を指定(UTF-8)
            var padding = "*";
            var length = 100;

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );
            xmlTree.SetAttributeValue("bytes", length);

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // パディングを右から適用するようにしてXMLの要素を書き込み
            xmlTree.SetAttributeValue("rpadding", padding);
            xmlToBinary.Write(stream, xmlTree);
            var bytes = stream.ReadAll();
            var targets = Encoding.UTF8.GetBytes(item);
            // パディングではないデータ本体の検査
            Assert.AreEqual(bytes.Length, length);
            CollectionAssert.AreEqual(bytes[..targets.Length], targets);
            // パディングの検査
            var paddingBytes = Enumerable.Repeat<byte>(Encoding.UTF8.GetBytes(padding)[0], length - targets.Length).ToArray();
            CollectionAssert.AreEqual(bytes[targets.Length..], paddingBytes);
        }

        [TestMethod]
        public void 明示的な左からのパディングを指定したテキスト要素の出力()
        {
            var item = "あいうえお";
            // 簡単のために1バイト文字を指定(UTF-8)
            var padding = "*";
            var length = 100;

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );
            xmlTree.SetAttributeValue("bytes", length);

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // パディングを右から適用するようにしてXMLの要素を書き込み
            xmlTree.SetAttributeValue("lpadding", padding);
            xmlToBinary.Write(stream, xmlTree);
            var bytes = stream.ReadAll();
            var targets = Encoding.UTF8.GetBytes(item);
            // パディングではないデータ本体の検査
            Assert.AreEqual(bytes.Length, length);
            CollectionAssert.AreEqual(bytes[(length - targets.Length)..], targets);
            // パディングの検査
            var paddingBytes = Enumerable.Repeat<byte>(Encoding.UTF8.GetBytes(padding)[0], length - targets.Length).ToArray();
            CollectionAssert.AreEqual(bytes[..(length - targets.Length)], paddingBytes);
        }

        [TestMethod]
        public void 空要素の出力()
        {
            var length = 10;
            var padding = 'a';
            var bytePadding = ((byte)padding);

            var xmlTree = new XElement("item",
                new XAttribute("bytes", length),
                new XElement("value", "")
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // パディングを指定しないで出力
            xmlToBinary.Write(stream, xmlTree);
            var bytes1 = stream.ReadAll();
            Assert.AreEqual(bytes1.Length, length);
            CollectionAssert.AreEqual(bytes1, new byte[length]);

            stream.SetLength(0);
            // パディングを指定して出力
            xmlTree.SetAttributeValue("padding", padding);
            xmlToBinary.Write(stream, xmlTree);
            var bytes2 = stream.ReadAll();
            Assert.AreEqual(bytes2.Length, length);
            CollectionAssert.AreEqual(bytes2, Enumerable.Repeat(bytePadding, length).ToArray());
        }

        [TestMethod]
        public void XPathにより計算されるテキスト要素の出力()
        {
            var item = "あいうえおあいうえお";
            // 兄弟ノードから「あ」の数をカウントするXPath(XPath 2.0の関数は使うことができなさそうのため1.0で再現)
            var xpath = "string-length(./preceding-sibling::item[1]/value) - string-length(translate(./preceding-sibling::item[1]/value, 'あ', ''))";
            // 「あ」の数は2つ
            var count = "2";

            var xmlValue = new XElement("value", xpath);
            xmlValue.SetAttributeValue("type", "xpath");
            var xmlSubTree = new XElement("item", xmlValue);
            _ = new XElement("items",
                // XPathで参照するための兄弟ノードの定義
                new XElement("item",
                    new XElement("value", item)
                ),
                xmlSubTree
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlSubTree);
            var byteArray = stream.ReadAll();
            CollectionAssert.AreEqual(byteArray, Encoding.UTF8.GetBytes(count));
        }

        [TestMethod]
        public void 時刻フォーマットによるテキスト要素の出力()
        {
            var item = "yyyyMMddHHmmss";

            var xmlTree = new XElement("item",
                new XElement("value",
                    new XAttribute("type", "current-time")
                    , item
                )
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            var begin = DateTime.Now;
            // beginのミリ秒以下を切り捨て
            begin = begin.AddTicks(-(begin.Ticks % TimeSpan.TicksPerSecond));
            xmlToBinary.Write(stream, xmlTree);
            var end = DateTime.Now;

            // 書き込んだ日時が[begin, end]の区間内であることの判定
            var dateTime = DateTime.ParseExact(Encoding.UTF8.GetString(stream.ReadAll()), item, CultureInfo.InvariantCulture);
            Assert.IsTrue(begin <= dateTime && dateTime <= end);
        }

        [TestMethod]
        public void 異常な時刻フォーマットの指定()
        {
            // サポートされていない分解能のミリ秒指定
            var item = "fffffffffffff";

            var xmlTree = new XElement("item",
                new XElement("value",
                    new XAttribute("type", "current-time")
                    , item
                )
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            try
            {
                xmlToBinary.Write(stream, xmlTree);
                Assert.Fail();
            }
            catch (FormatException)
            {
                // 書き込み異常が生じたときにStreamへの書き込みが行われないこと
                Assert.AreEqual(stream.Length, 0);
            }
        }

        [TestMethod]
        public void Transformにより変換される()
        {
            var item = "あいうえお";
            var transformerName = "trans";

            var xmlTree = new XElement("item",
                new XElement("value", item),
                new XElement("transform", transformerName)
            );

            var transformerControl = new StubTransformerControl();
            Assert.AreEqual(transformerControl.Reference, null);

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(transformerControl, new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(new StubTransformer().Transform(item)));

            // 前回参照された変換器の確認
            Assert.AreEqual(transformerControl.Reference, transformerName);
        }

        [TestMethod]
        public void Transformにより変換されない()
        {
            var transformerName = "trans";

            var xmlTree = new XElement("item",
                new XElement("transform", transformerName)
            );

            var transformerControl = new StubTransformerControl();
            Assert.AreEqual(transformerControl.Reference, null);

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(transformerControl, new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(""));

            // 変換器の参照が生じていないことの確認
            Assert.AreEqual(transformerControl.Reference, null);
        }

        [TestMethod]
        public void 書き込み先のオフセットの指定()
        {
            var item1 = "あいうえお";
            var item2 = "か";
            var to = "あかうえおあいうえお";

            var xmlTree1 = new XElement("item",
                new XElement("value", item1)
            );
            var xmlTree2 = new XElement("item",
                new XElement("value", item2)
            );
            xmlTree2.SetAttributeValue("offset", "3");

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            xmlToBinary.Write(stream, xmlTree1);
            xmlToBinary.Write(stream, xmlTree2);
            xmlToBinary.Write(stream, xmlTree1);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(to));
        }

        [TestMethod]
        public void 異常なオフセットの指定()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // 最大値を超えるオフセットの指定
            xmlTree.SetAttributeValue("offset", "9223372036854775808");
            try
            {
                xmlToBinary.Write(stream, xmlTree);
                Assert.Fail();
            }
            catch (OverflowException)
            {
                // 書き込み異常が生じたときにStreamへの書き込みが行われないこと
                Assert.AreEqual(stream.Length, 0);
            }

            // 負のオフセットの指定
            xmlTree.SetAttributeValue("offset", "-1");
            try
            {
                xmlToBinary.Write(stream, xmlTree);
                Assert.Fail();
            }
            catch (IOException)
            {
                // 書き込み異常が生じたときにStreamへの書き込みが行われないこと
                Assert.AreEqual(stream.Length, 0);
            }
        }

        [TestMethod]
        public void オフセットのロールバックの確認()
        {
            var item = "あいうえお";

            var xmlTree = new XElement("item",
                new XElement("value", item)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // 適当に書き込み行い、オフセットを変更
            xmlToBinary.Write(stream, xmlTree);
            var offset = stream.Position;

            // 確実に異なるオフセットの指定
            xmlTree.SetAttributeValue("offset", offset / 2);
            try
            {
                // オフセットに関する以外の異常を起こす
                xmlTree.SetAttributeValue("bytes", "abc");
                xmlToBinary.Write(stream, xmlTree);
                Assert.Fail();
            }
            catch
            {
                // 書き込み異常が生じたときにStreamへの書き込みが行われないこと
                Assert.AreEqual(stream.Length, offset);
                // オフセットがロールバックされること
                Assert.AreEqual(stream.Position, offset);
            }
        }

        [TestMethod]
        public void カウンタ値の取得()
        {
            var counterName = "count";

            var xmlTree = new XElement("item",
                new XElement("value", new XAttribute("type", "counter"), counterName)
            );

            var counter = new StubCounter();
            Assert.AreEqual(counter.Reference, null);

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), counter, new StubXPathResolver(), []);

            // XMLの要素を書き込み
            for (int i = 0; i < 3; ++i)
            {
                stream.SetLength(0);

                xmlToBinary.Write(stream, xmlTree);
                // カウンタの実行毎に値が変化する(ステートレスではない)ことの確認
                CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes($"{i}"));

                // 前回参照されたカウンタの確認
                Assert.AreEqual(counter.Reference, counterName);
            }
        }

        [TestMethod]
        public void 自動採番値の取得()
        {
            long init = 3;

            var xmlTree = new XElement("item",
                new XElement("value", new XAttribute("type", "auto-increment"), init)
            );

            using var stream = new MemoryStream();
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), []);

            // XMLの要素を書き込み
            for (int i = 0; i < 3; ++i)
            {
                stream.SetLength(0);

                xmlToBinary.Write(stream, xmlTree);
                // カウンタの実行毎に値が変化する(ステートレスではない)ことの確認
                CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes($"{init + i}"));
            }
        }

        [TestMethod]
        public void 外部パラメータの取得()
        {
            string key = "key";
            string value = "value";

            var xmlTree = new XElement("item",
                new XElement("value", new XAttribute("type", "external"), key)
            );

            using var stream = new MemoryStream();
            var dic = new Dictionary<string, string> { { key, value } };
            var xmlToBinary = new XmlToBinary(new StubTransformerControl(), new StubCounter(), new StubXPathResolver(), dic);

            xmlToBinary.Write(stream, xmlTree);
            CollectionAssert.AreEqual(stream.ReadAll(), Encoding.UTF8.GetBytes(value));

            stream.SetLength(0);
            // 存在しない外部パラメータへのアクセス
            dic.Clear();
            xmlToBinary.Write(stream, xmlTree);
            Assert.AreEqual(stream.Length, 0);
        }
    }
}
