using CsvToBinary.BuiltIn;
using System.Xml.Linq;

namespace tests.BuiltIn
{
    [TestClass]
    public class CharaTransformerTests
    {
        [TestMethod]
        public void 固定文字列による単純文字列変換()
        {
            var from = "あいうえお";
            var to = "かきくけこ";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", from[0]),
                    new XAttribute("to", to[0])
                ),
                new XElement("map",
                    new XAttribute("from", from[1]),
                    new XAttribute("to", to[1])
                ),
                new XElement("map",
                    new XAttribute("from", from[2]),
                    new XAttribute("to", to[2])
                ),
                new XElement("map",
                    new XAttribute("from", from[3]),
                    new XAttribute("to", to[3])
                ),
                new XElement("map",
                    new XAttribute("from", from[4]),
                    new XAttribute("to", to[4])
                )
            ));

            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void 固定文字列によるファイルからの単純文字列変換()
        {
            var from = "あいうえお";
            var to = "かきくけこ";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", from[0]),
                    new XAttribute("to-file", to[0] + ".txt")
                ),
                new XElement("map",
                    new XAttribute("from", from[1]),
                    new XAttribute("to-file", to[1] + ".txt")
                ),
                new XElement("map",
                    new XAttribute("from", from[2]),
                    new XAttribute("to-file", to[2] + ".txt")
                ),
                new XElement("map",
                    new XAttribute("from", from[3]),
                    new XAttribute("to-file", to[3] + ".txt")
                ),
                new XElement("map",
                    new XAttribute("from", from[4]),
                    new XAttribute("to-file", to[4] + ".txt")
                )
            ));

            // ファイルを読み込む関数は単にファイル名部を抽出するようにする
            var transformer = new CharaTransformer(xmlTree, (x, y) => Path.GetFileNameWithoutExtension(x));

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void 正規表現による単純文字列変換()
        {
            // 「あいうえお」と「かきくけこ」を交差させた文字列
            var from = "あかいきうくえけおこ";
            var to = "かきくけこ";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from-regex", $".(.)"),
                    // 任意の2文字のマッチに関して2文字目を取り出す
                    new XAttribute("to", "{1}")
                )
            ));

            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void 正規表現によるファイルからの単純文字列変換()
        {
            // 「あいうえお」と「かきくけこ」を交差させた文字列
            var from = "あかいきうくえけおこ";
            var to = "かきくけこ";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from-regex", $".(.)"),
                    // 任意の2文字のマッチに関して2文字目を取り出す
                    new XAttribute("to-file", "{1}.txt")
                )
            ));

            // ファイルを読み込む関数は単にファイル名部を抽出するようにする
            var transformer = new CharaTransformer(xmlTree, (x, y) => Path.GetFileNameWithoutExtension(x));

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void ロンゲストマッチによる単純文字列変換()
        {
            var from = "あああああ";
            var to = "かく";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あああ"),
                    new XAttribute("to", "か")
                ),
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to", "き")
                ),
                new XElement("map",
                    new XAttribute("from", "ああ"),
                    new XAttribute("to", "く")
                )
            ));

            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void ファイル読み込み結果がキャッシュされることの確認()
        {
            var from = "あああああ";
            var to = "かかかかか";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to-file", "か")
                )
            ));

            // ファイルを読み込む関数は単にファイル名部を抽出するようにする
            var cnt = 0;
            var transformer = new CharaTransformer(xmlTree, (x, y) =>
            {
                ++cnt;
                return Path.GetFileNameWithoutExtension(x);
            });

            Assert.AreEqual(to, transformer.Transform(from));
            // ファイル読み込みが1度のみ発生したことの確認
            Assert.AreEqual(1, cnt);
        }

        [TestMethod]
        public void 正規表現の評価順序が宣言順であることの確認()
        {
            var from = "ああいあい";
            var to = "かきくき";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from-regex", $"ああ"),
                    new XAttribute("to", "か")
                ),
                new XElement("map",
                    new XAttribute("from-regex", $"い"),
                    new XAttribute("to", "き")
                ),
                new XElement("map",
                    new XAttribute("from-regex", $"."),
                    new XAttribute("to", "く")
                )
            ));

            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void 固定文字列による単純文字列変換と正規表現による単純文字列変換の混在()
        {
            var from = "あいうえお";
            var to = "かいくえこ";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to", "か")
                ),
                new XElement("map",
                    new XAttribute("from", "う"),
                    new XAttribute("to", "く")
                ),
                new XElement("map",
                    new XAttribute("from", "お"),
                    new XAttribute("to", "こ")
                ),
                new XElement("map",
                    new XAttribute("from-regex", $"."),
                    // 変換なし
                    new XAttribute("to", "{0}")
                )
            ));

            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual(to, transformer.Transform(from));
        }

        [TestMethod]
        public void 有効なFrom属性が存在しない()
        {
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("to", "か")
                )
            ));

            try
            {
                var transformer = new CharaTransformer(xmlTree, (x, y) => "");
                Assert.Fail();
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void 有効なTo属性が存在しない()
        {

            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あ")
                )
            ));

            try
            {
                var transformer = new CharaTransformer(xmlTree, (x, y) => "");
                Assert.Fail();
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void 不正な正規表現の指定()
        {
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from-regex", "(."),
                    new XAttribute("to", "{0}")
                )
            ));

            try
            {
                var transformer = new CharaTransformer(xmlTree, (x, y) => "");
                Assert.Fail();
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void 変換規則が存在しない()
        {
            var from = "あいうえお";
            var to = "かきくけこ";
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", from[0]),
                    new XAttribute("to", to[0])
                )
            ));

            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            try
            {
                Assert.AreEqual(to, transformer.Transform(from));
            }
            catch (TransformException) { }
        }

        private static XElement Map(string from, string to)
        {
            return new XElement("map", new XAttribute("from", from), new XAttribute("to", to));
        }

        [TestMethod]
        public void 固定文字列による逆変換()
        {
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to", "か")
                ),
                new XElement("map",
                    new XAttribute("from", "い"),
                    new XAttribute("to", "き")
                ),
                new XElement("map",
                    new XAttribute("from", "う"),
                    new XAttribute("to", "く")
                )
            ));
            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual("あいう", transformer.InverseTransform("かきく"));
            Assert.AreEqual("うあ", transformer.InverseTransform(transformer.Transform("うあ")));
        }

        [TestMethod]
        public void ファイルからの変換の逆変換()
        {
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to-file", "a.txt")
                ),
                new XElement("map",
                    new XAttribute("from", "い"),
                    new XAttribute("to", "き")
                )
            ));
            var transformer = new CharaTransformer(xmlTree, (path, encoding) => path.EndsWith("a.txt") ? "ABC" : "");

            Assert.AreEqual("あいあ", transformer.InverseTransform("ABCきABC"));
        }

        [TestMethod]
        public void 長さの異なる変換先のロンゲストマッチによる逆変換()
        {
            var xmlTree = new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to", "ab")
                ),
                new XElement("map",
                    new XAttribute("from", "い"),
                    new XAttribute("to", "abc")
                ),
                new XElement("map",
                    new XAttribute("from", "う"),
                    new XAttribute("to", "c")
                )
            ));
            var transformer = new CharaTransformer(xmlTree, (x, y) => "");

            Assert.AreEqual("abc", transformer.Transform("あう"));
            Assert.AreEqual("い", transformer.InverseTransform("abc"));
        }

        [TestMethod]
        public void 変換先が重複する場合は最初に宣言された変換元に逆変換される()
        {
            var duplicated = new CharaTransformer(new XDocument(new XElement("transform",
                new XElement("map",
                    new XAttribute("from", "い"),
                    new XAttribute("to", "x")
                ),
                new XElement("map",
                    new XAttribute("from", "あ"),
                    new XAttribute("to", "x")
                ),
                new XElement("map",
                    new XAttribute("from", "う"),
                    new XAttribute("to", "y")
                )
            )), (x, y) => "");
            Assert.AreEqual("いう", duplicated.InverseTransform("xy"));

            // 宣言順を入れ替えると結果も入れ替わる
            var reversed = new CharaTransformer(new XDocument(new XElement("transform",
                Map("あ", "x"), Map("い", "x")
            )), (x, y) => "");
            Assert.AreEqual("あ", reversed.InverseTransform("x"));
        }

        [TestMethod]
        public void 空文字列への変換は逆変換できない()
        {
            // 空文字列への変換
            var empty = new CharaTransformer(new XDocument(new XElement("transform",
                Map("あ", "x"), Map("い", "")
            )), (x, y) => "");
            Assert.ThrowsException<TransformException>(() => empty.InverseTransform("xy"));
        }

        [TestMethod]
        public void 正規表現による変換の結果は逆変換できない()
        {
            var transformer = new CharaTransformer(new XDocument(new XElement("transform",
                new XElement("map", new XAttribute("from-regex", "[0-9]"), new XAttribute("to", "#"))
            )), (x, y) => "");

            Assert.AreEqual("#", transformer.Transform("1"));
            Assert.ThrowsException<TransformException>(() => transformer.InverseTransform("#"));
        }

    }
}