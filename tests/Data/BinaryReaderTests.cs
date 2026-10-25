using System.Text;
using System.Xml.Linq;
using CsvToBinary.BuiltIn;
using CsvToBinary.Xml;
using tests.Xml;
using BinaryReader = CsvToBinary.Data.BinaryReader;

namespace tests.Data
{
    [TestClass]
    public class BinaryReaderTests
    {
        /// <summary>
        /// バイナリ列から1つのitemを読み込む
        /// </summary>
        private static string Read(byte[] data, XElement item)
        {
            using var stream = new MemoryStream(data);
            var reader = new BinaryReader(stream, new StubXPathResolver(), new StubTransformerControl(), []);
            return reader.GetData(item.Attribute("name")!.Value, item);
        }

        [TestMethod]
        public void UTF8のテキスト要素の読み込み()
        {
            var item = new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 15)
            );
            Assert.AreEqual("あいうえお", Read(Encoding.UTF8.GetBytes("あいうえお"), item));
            Assert.AreEqual("あいうえお", item.Attribute("result")?.Value);
            Assert.AreEqual("15", item.Attribute("result-bytes")?.Value);
        }

        [TestMethod]
        public void 右側のデフォルトパディングの除去()
        {
            var data = new byte[] { (byte)'a', (byte)'b', 0, 0 };
            Assert.AreEqual("ab", Read(data, new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 4)
            )));
        }

        [TestMethod]
        public void 左側のパディングの除去()
        {
            Assert.AreEqual("12", Read(Encoding.UTF8.GetBytes("0012"), new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 4),
                new XAttribute("lpadding", "0")
            )));
        }

        [TestMethod]
        public void 複数バイトのパディングの除去()
        {
            // パディングは繰り返され領域に収まらない分は切り詰められる
            Assert.AreEqual("x", Read(Encoding.UTF8.GetBytes("xaabaa"), new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 6),
                new XAttribute("rpadding", "aab")
            )));
            Assert.AreEqual("x", Read(Encoding.UTF8.GetBytes("aabaax"), new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 6),
                new XAttribute("lpadding", "aab")
            )));
        }

        [TestMethod]
        public void xbytesによる長さの解決()
        {
            var format = new XElement("format",
                new XElement("item", new XAttribute("name", "len"), new XAttribute("bytes", 1), new XAttribute("encoding", "decimal")),
                new XElement("item", new XAttribute("name", "body"), new XAttribute("xbytes", "string(../item[@name='len']/@result)"))
            );
            // 一般的な可変長データフォーマット(長さ+実データ)
            using var stream = new MemoryStream([3, (byte)'a', (byte)'b', (byte)'c']);
            var reader = new BinaryReader(stream, new StubXPathResolver(), new StubTransformerControl(), []);

            Assert.AreEqual("3", reader.GetData("len", format.Elements().ElementAt(0)));
            Assert.AreEqual("abc", reader.GetData("body", format.Elements().ElementAt(1)));
        }

        [TestMethod]
        public void bytesが0の項目かつ評価可能な場合はスキップされない()
        {
            var item = new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 0),
                new XElement("default-value", new XAttribute("type", "external"), "external")
            );
            using var stream = new MemoryStream([1, 2]);
            var reader = new BinaryReader(stream, new StubXPathResolver(), new StubTransformerControl(), new() { ["external"] = "val" });

            Assert.AreEqual("val", reader.GetData("a", item));
            // バイナリは読み進めない
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public void ReadChunkは未読のデータの有無を返す()
        {
            using var stream = new MemoryStream([1, 2]);
            var reader = new BinaryReader(stream, new StubXPathResolver(), new StubTransformerControl(), []);

            Assert.IsFalse(reader.Valid());
            Assert.IsTrue(reader.ReadChunk());
            Assert.IsTrue(reader.Valid());
            reader.GetData("a", new XElement("item", new XAttribute("name", "a"), new XAttribute("bytes", 2), new XAttribute("encoding", "hexadecimal")));
            Assert.IsFalse(reader.ReadChunk());
            Assert.IsFalse(reader.Valid());
        }

        /// <summary>
        /// writerで切り替えるStringWriterがCsvReaderの利用を前提としている側面があるためそのうち動作を変更する可能性がある
        /// </summary>
        [TestMethod]
        public void writer要素内の項目は評価可能であれば評価される()
        {
            var format = new XElement("format",
                new XElement("writer",
                    // bytesの指定があってもバイナリは読み込まない
                    new XElement("item",
                        new XAttribute("name", "a"),
                        new XAttribute("bytes", 2),
                        new XElement("default-value", new XAttribute("type", "external"), "external")
                    ),
                    new XElement("item",
                        new XAttribute("name", "b"),
                        new XElement("default-value", new XAttribute("type", "xpath"), "concat(../item[@name='a']/@result, '!')")
                    ),
                    new XElement("item",
                        new XAttribute("name", "c"),
                        new XElement("default-value", "固定値")
                    ),
                    // 副作用のある評価は実施されない
                    new XElement("item",
                        new XAttribute("name", "d"),
                        new XElement("default-value",
                        new XAttribute("type", "counter"), "file-name")
                    ),
                    new XElement("item",
                        new XAttribute("name", "e"),
                        new XElement("default-value",
                        new XAttribute("type", "external"), "undefined")
                    )
                )
            );
            var items = format.Descendants("item").ToList();
            using var stream = new MemoryStream([1, 2]);
            var reader = new BinaryReader(stream, new StubXPathResolver(), new StubTransformerControl(), new() { ["external"] = "val" });

            Assert.AreEqual("val", reader.GetData("a", items[0]));
            Assert.AreEqual("val!", reader.GetData("b", items[1]));
            Assert.AreEqual("固定値", reader.GetData("c", items[2]));
            Assert.AreEqual("", reader.GetData("d", items[3]));
            Assert.AreEqual("", reader.GetData("e", items[4]));
            Assert.AreEqual(0, stream.Position);
        }

        /// <summary>
        /// 名称ごとに変換器を返すITransformerControlのスタブ
        /// </summary>
        private class StubControl(Dictionary<string, ITransformer> transformers) : ITransformerControl
        {
            public ITransformer Get(string name)
            {
                return transformers[name];
            }
        }

        [TestMethod]
        public void transformの逆変換()
        {
            var control = new StubControl(new() { ["t.xml"] = 
                new CharaTransformer(
                    new XDocument(new XElement("transform",
                        new XElement("map",
                            new XAttribute("from", "あ"),
                            new XAttribute("to", "A")
                        ),
                        new XElement("map",
                            new XAttribute("from", "い"),
                            new XAttribute("to", "B")
                        )
                    ))
                , (x, y) => "")
            });
            var item = new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 3),
                new XElement("transform", "t.xml")
            );
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("ABA"));
            var reader = new BinaryReader(stream, new StubXPathResolver(), control, []);

            Assert.AreEqual("あいあ", reader.GetData("a", item));
            Assert.AreEqual("あいあ", item.Attribute("result")?.Value);
        }

        [TestMethod]
        public void 複数のtransformは宣言の逆順に逆変換される()
        {
            // 変換は宣言順(あ→A→x)に適用されるため逆変換はx→A→あの順になる
            var control = new StubControl(new()
            {
                ["t1.xml"] = new CharaTransformer(
                    new XDocument(new XElement("transform",
                        new XElement("map",
                            new XAttribute("from", "あ"),
                            new XAttribute("to", "A")
                        )
                    ))
                , (x, y) => ""),
                ["t2.xml"] = new CharaTransformer(
                    new XDocument(new XElement("transform",
                        new XElement("map",
                            new XAttribute("from", "A"),
                            new XAttribute("to", "x")
                        )
                    ))
                , (x, y) => ""),
            });
            var item = new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 1),
                new XElement("transform", "t1.xml"),
                new XElement("transform", "t2.xml")
            );
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));
            var reader = new BinaryReader(stream, new StubXPathResolver(), control, []);

            Assert.AreEqual("あ", reader.GetData("a", item));
        }

        [TestMethod]
        public void パディングはtransformの逆変換より先に除去される()
        {
            var control = new StubControl(new() { ["t.xml"] =
                new CharaTransformer(
                    new XDocument(new XElement("transform",
                        new XElement("map",
                            new XAttribute("from", "あ"),
                            new XAttribute("to", "A")
                        )
                    ))
                , (x, y) => "")
            });
            var item = new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 4),
                new XAttribute("lpadding", "0"),
                new XElement("transform", "t.xml")
            );
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("000A"));
            var reader = new BinaryReader(stream, new StubXPathResolver(), control, []);

            Assert.AreEqual("あ", reader.GetData("a", item));
        }

        [TestMethod]
        public void 逆変換できないtransformは変換失敗となる()
        {
            var item = new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 1),
                new XElement("transform", "t.xml")
            );

            // 逆変換規則が存在しない
            using var stream = new MemoryStream([(byte)'Z']);
            var reader = new BinaryReader(stream, new StubXPathResolver(), new StubControl(new() { ["t.xml"] =
                new CharaTransformer(
                    new XDocument(new XElement("transform",
                        new XElement("map",
                            new XAttribute("from", "あ"),
                            new XAttribute("to", "A")
                        )
                    ))
                , (x, y) => "")
            }), []);
            Assert.ThrowsException<InputDataException>(() => reader.GetData("a", item));
        }

        [TestMethod]
        public void 逆変換できない項目は変換失敗となる()
        {
            // バイト数が不定
            Assert.ThrowsException<InputDataException>(() => Read([1], new XElement("item",
                new XAttribute("name", "a")
            )));
            // バイト数が負数
            Assert.ThrowsException<InputDataException>(() => Read([1], new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", -1)
            )));
            // データ長が不足
            Assert.ThrowsException<InputDataException>(() => Read([1], new XElement("item",
                new XAttribute("name", "a"),
                new XAttribute("bytes", 2)
            )));
        }
    }
}
