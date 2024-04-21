using CsvHelper;
using Data;
using System.Text;
using System.Xml.Linq;
using Xml;
using StringWriter = Data.StringWriter;

namespace tests.Xml
{
    /// <summary>
    /// IXmlToBinaryについてのスタブ
    /// </summary>
    public class StubXmlToBinary : IXmlToBinary
    {
        public string GetString(XElement element)
        {
            // 書き込み対象の文字列としての値
            string strValue = "";

            foreach (XElement child in element.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "value":
                        strValue = child.Value;
                        break;
                    case "default-value":
                        if (strValue.Length == 0)
                        {
                            strValue = child.Value;
                        }
                        break;
                }
            }

            return strValue;
        }

        public void Write(Stream stream, XElement element)
        {
            var strValue = this.GetString(element);
            stream.Write(Encoding.UTF8.GetBytes(strValue));
        }
    }

    /// <summary>
    /// IDataReaderについてのスタブ
    /// </summary>
    public class StubDataReader : IDataReader
    {
        /// <summary>
        /// 現在読み込んでいる行に関するスタック
        /// </summary>
        private readonly Stack<string[]> stack = new();

        /// <summary>
        /// カラム名とそのインデックスの対応付け
        /// </summary>
        private readonly Dictionary<string, int> columnMap = [];

        /// <summary>
        /// 読み込み対象のデータ
        /// </summary>
        private readonly string[][] dataArray = [];

        /// <summary>
        /// 現在読み込み中の行
        /// </summary>
        private int currentRow = 0;

        public StubDataReader(string[] headerArray, string[][] dataArray)
        {
            // ヘッダ情報のセットアップ
            for (int i = 0; i < headerArray.Length; ++i)
            {
                this.columnMap[headerArray[i]] = i;
            }
            // データの構築
            this.dataArray = dataArray;
            // 空のカレントデータのセット
            this.stack.Push([]);
        }

        public string GetData(string key, XElement item)
        {
            if (this.columnMap.TryGetValue(key, out int index) && this.stack.TryPeek(out string[]? peek) && index < peek.Length)
            {
                return peek[index];
            }
            return "";
        }

        public void Pop() {
            this.stack.Pop();
        }

        public void Push() {
            this.stack.Push([]);
        }

        public bool ReadChunk()
        {
            if (this.currentRow < this.dataArray.Length)
            {
                // 次の行へ解析対象を遷移する
                this.stack.Pop();
                this.stack.Push(this.dataArray[this.currentRow]);
                ++this.currentRow;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 最後にReadChunkを呼び出した際の戻り値の取得
        /// </summary>
        /// <returns>最後にReadChunkを呼び出した際の戻り値(未呼び出しの場合はfalse)</returns>
        public bool Valid()
        {
            return this.currentRow != 0 && this.currentRow - 1 < this.dataArray.Length;
        }

        /// <summary>
        /// 現在読み込んでいる処理単位に対して割り振られるIDの取得
        /// </summary>
        /// <returns></returns>
        public int GetChunkId()
        {
            return this.currentRow;
        }

        /// <summary>
        /// 現在の読み込み状況をリセットする
        /// </summary>
        public void Reset() {
            this.stack.Clear();
            this.stack.Push([]);
            this.currentRow = 0;
        }

        /// <summary>
        /// writerに書き込まれた内容がthisに含まれているとみなせるか判定
        /// </summary>
        /// <param name="writer">書き込まれた内容</param>
        /// <param name="offset">行のオフセット</param>
        /// <param name="size">比較する行数</param>
        /// <param name="inv">includeの関係を逆にする</param>
        /// <returns>書き込まれているとみなされる場合にtrue</returns>
        public bool Included(StubDataWriter writer, int offset = 0, int size = 0, bool inv = false)
        {
            if (size == 0)
            {
                size = this.dataArray.Length;
            }

            // 行数の比較
            if (this.dataArray.Length != size)
            {
                return false;
            }

            // ヘッダ名に関するインデックスの構築
            var indexMap = new Dictionary<int, int>();
            foreach (var map in this.columnMap)
            {
                var index = writer.GetIndex(map.Key);
                if (index == -1)
                {
                    // writerに存在しないインデックスが存在する
                    return false;
                }
                indexMap.Add(map.Value, index);
            }

            for (int i = 0; i < size; ++i)
            {
                var row = this.dataArray[i];
                var writerRow = writer.DataArray[offset + i];
                for (int j = 0; j < row.Length; ++j)
                {
                    // invによって包含関係を計算する
                    bool cond = inv ? row[j].Length != 0 : indexMap[j] < writerRow.Count && writerRow[indexMap[j]].Length != 0;
                    if (cond)
                    {
                        // 値が空でない場合に比較
                        if (row[j] != writerRow[indexMap[j]])
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// データ部の行数の取得
        /// </summary>
        public int RowCount => this.dataArray.Length;
    }

    /// <summary>
    /// IDataReaderについてのスタブ
    /// </summary>
    public class StubDataWriter : IDataWriter
    {
        /// <summary>
        /// 現在書き込んでいる行に関するスタック
        /// </summary>
        private readonly Stack<List<string>> stack = new();

        /// <summary>
        /// カラム名とそのインデックスの対応付け
        /// </summary>
        private readonly Dictionary<string, int> columnMap = [];

        /// <summary>
        /// 書き込み済みのデータ
        /// </summary>
        private readonly List<List<string>> dataArray = [];

        /// <summary>
        /// 書き込み可能なデータ
        /// </summary>
        private readonly List<List<List<string>>> tempDataArray = [];

        /// <summary>
        /// XMLをStreamへ書き出す処理が記載されたインスタンス
        /// </summary>
        private readonly IXmlToBinary xmlToBinary;

        /// <summary>
        /// pushやpopにおけるネストの深さ
        /// </summary>
        private int nestLevel = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="xmlToBinary">XMLをStreamへ書き出す処理が記載されたインスタンス</param>
        public StubDataWriter(IXmlToBinary xmlToBinary)
        {
            this.xmlToBinary = xmlToBinary;
        }

        public void SetData(string key, XElement item)
        {
            if (!this.columnMap.TryGetValue(key, out int index))
            {
                // ヘッダ情報の追加
                index = this.columnMap.Count;
                this.columnMap[key] = index;
            }
            if (!this.stack.TryPeek(out List<string>? peek))
            {
                throw new InvalidOperationException("書き込み不可");
            }
            if (peek.Count <= index)
            {
                // 要素が足りないときは追加
                peek.AddRange(Enumerable.Repeat("", index + 1 - peek.Count));
            }
            // データの書き込み
            peek[index] = this.xmlToBinary.GetString(item);
        }

        public void Pop()
        {
            // 書き込み可能データをマージする
            // this.nestLevel - 1の深さはPop後も継続して書き込まれるため
            // そこへはマージしないようにする
            if (this.tempDataArray.Count > this.nestLevel - 1)
            {
                if (this.tempDataArray.Count > 1)
                {
                    // まだ本体に書き込むことができない場合
                    this.tempDataArray[^2].AddRange(this.tempDataArray[^1]);
                    this.tempDataArray.RemoveAt(this.nestLevel - 1);
                }
                else
                {
                    // 本体に書き込む
                    this.dataArray.AddRange(this.tempDataArray[^1]);
                    this.tempDataArray.Clear();
                }
                // ヘッダ行とのマージについては実装略
            }
            --this.nestLevel;
            this.stack.Pop();
        }

        public void Push()
        {
            ++this.nestLevel;
            this.stack.Push([]);
        }

        public void WriteChunk()
        {
            if (this.stack.TryPeek(out List<string>? peek))
            {
                List<List<string>> targetArray = [];
                if (this.nestLevel > 1)
                {
                    // ネストの深さが深いときは一時的なデータ列へ書き込み
                    var size = this.tempDataArray.Count;
                    if (size < this.nestLevel - 1)
                    {
                        // 書き込み可能だがまだ書き込んでいないデータの配列サイズが不足しているときは拡張
                        this.tempDataArray.AddRange(Enumerable.Repeat<List<List<string>>>([], this.nestLevel - 1 - size));
                    }
                    targetArray = this.tempDataArray[this.nestLevel - 2];
                }
                else
                {
                    // ネストが最低限の場合は直接書き出す
                    targetArray = this.dataArray;
                }
                targetArray.Add(peek);

                // this.tempDataArrayのより深いところへの書き込みをマージする
                for (int i = this.nestLevel - 1; i < this.tempDataArray.Count; ++i)
                {
                    targetArray.AddRange(this.tempDataArray[i]);
                }
                if (this.tempDataArray.Count > this.nestLevel - 1) {
                    this.tempDataArray.RemoveRange(this.nestLevel - 1, this.tempDataArray.Count - (this.nestLevel - 1));
                }

                // スタックへの書き込み状況をリセットする
                this.stack.Pop();
                this.stack.Push([]);
            }
        }

        /// <summary>
        /// Chunkに関するスタックの深さの取得
        /// </summary>
        public int Depth()
        {
            return this.nestLevel;
        }

        /// <summary>
        /// 現在の書き込み状況をリセットする
        /// </summary>
        public void Reset()
        {
            this.stack.Clear();
            this.stack.Push([]);
        }

        /// <summary>
        /// キーに対応するインデックスの取得
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetIndex(string key)
        {
            if (this.columnMap.TryGetValue(key, out int index))
            {
                return index;
            }
            return -1;
        }

        public void Dispose() { }

        public List<List<string>> DataArray => dataArray;
    }

    [TestClass]
    public class XmlTraverserTests
    {
        [TestMethod]
        public void Itemノード群の変換()
        {
            var name1 = "name1";
            var name2 = "name2";
            var name3 = "name3";
            var item1 = "あ";
            var item2 = "い";
            var item3 = "う";
            // データの構築のためのXMLの定義
            var xmlTree = new XDocument(
                new XElement("format",
                    // IDataReaderによる値が設定される場合
                    new XElement(
                        "item",
                        new XAttribute("name", name1),
                        new XElement("value")
                    ),
                    // IDataReaderによる値が設定されない場合
                    new XElement(
                        "item",
                        new XAttribute("name", name2),
                        new XElement("default-value", item2)
                    ),
                    // IDataReaderによる値が無視される場合
                    new XElement(
                        "item",
                        new XAttribute("name", name3),
                        new XElement("value", ""),
                        new XElement("default-value", item3)
                    )
                )
            );
            // XMLにディスパッチするデータの定義
            var reader = new StubDataReader(
                [name1, name3],
                [[item1, ""]]
            );
            // 読み込み可能な状態にしておく
            reader.ReadChunk();

            var xmlToBinary = new StubXmlToBinary();
            var xmlTraverser = new XmlTraverser(
                new StubXPathResolver(),
                xmlToBinary,
                (string path) => new XDocument(),
                (string type, string name, IXmlToBinary xmlToBinary) => new StringWriter(new MemoryStream(), xmlToBinary),
                []
            );

            using var writer = new StubDataWriter(xmlToBinary);

            xmlTraverser.Traversal(writer, (reader, xmlTree), []).ToArray();
            // readerにより設定される値の比較
            Assert.IsTrue(reader.Included(writer, 0, 0, true));
            // readerにより設定されない値の比較
            Assert.AreEqual(writer.DataArray[0][1], item2);
            Assert.AreEqual(writer.DataArray[0][2], item3);
        }

        [TestMethod]
        public void Itemsノード群の変換()
        {
            var itemsName1 = "items1";
            var name1 = "name1";
            var name2 = "name2";
            var name3 = "name3";
            var name4 = "name4";
            var name5 = "name5";
            var item1 = "あ";
            var item2 = "い";
            var item3 = "う";
            // データの構築のためのXMLの定義
            var xmlTree = new XDocument(
                new XElement("format",
                    // itemsの前後に挟むitems
                    new XElement(
                        "item",
                        new XAttribute("name", name1)
                    ),
                    // 名前付きのitems
                    new XElement(
                        "items",
                        new XAttribute("name", itemsName1),
                        new XElement(
                            "item",
                            new XAttribute("name", name1)
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name2)
                        )
                    ),
                    // itemsの前後に挟むitems
                    new XElement(
                        "item",
                        new XAttribute("name", name2)
                    ),
                    // 名前なしのitems
                    new XElement(
                        "items",
                        new XElement(
                            "item",
                            new XAttribute("name", name4)
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name5)
                        )
                    ),
                    // itemsの前後に挟むitems
                    new XElement(
                        "item",
                        new XAttribute("name", name3)
                    )
                )
            );
            // XMLにディスパッチするデータの定義
            var reader = new StubDataReader(
                [name1, name2, name3, $"{itemsName1}/{name1}", $"{itemsName1}/{name2}", name4, name5],
                [[item1, item2, item3, $"{itemsName1}/{item1}", $"{itemsName1}/{item2}", $"{name4}/{item1}", $"{name5}/{item3}"]]
            );
            // 読み込み可能な状態にしておく
            reader.ReadChunk();

            var xmlToBinary = new StubXmlToBinary();
            var xmlTraverser = new XmlTraverser(
                new StubXPathResolver(),
                xmlToBinary,
                (string path) => new XDocument(),
                (string type, string name, IXmlToBinary xmlToBinary) => new StringWriter(new MemoryStream(), xmlToBinary),
                []
            );

            using var writer = new StubDataWriter(xmlToBinary);

            xmlTraverser.Traversal(writer, (reader, xmlTree), []).ToArray();
            // readerにより設定される値の比較
            Assert.IsTrue(reader.Included(writer));
        }

        [TestMethod]
        public void Itemsノードのネストの確認()
        {
            var itemsName1 = "items1";
            var itemsName2 = "items2";
            var name1 = "name1";
            var name2 = "name2";
            var name3 = "name3";
            var item1 = "あ";
            var item2 = "い";
            var item3 = "う";
            // データの構築のためのXMLの定義
            var xmlTree = new XDocument(
                new XElement("format",
                    new XElement(
                        "items",
                        new XAttribute("name", itemsName1),
                        new XElement(
                            "item",
                            new XAttribute("name", name1)
                        ),
                        // itemsの中にitemsを記述する
                        new XElement(
                            "items",
                            new XAttribute("name", itemsName2),
                            new XElement(
                                "item",
                                new XAttribute("name", name1)
                            ),
                            new XElement(
                                "item",
                                new XAttribute("name", name3)
                            )
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name2)
                        )
                    )
                )
            );
            // XMLにディスパッチするデータの定義
            var reader = new StubDataReader(
                [$"{itemsName1}/{name1}", $"{itemsName1}/{name2}", $"{itemsName1}/{itemsName2}/{name1}", $"{itemsName1}/{itemsName2}/{name3}"],
                [[$"{itemsName1}/{item1}", $"{itemsName1}/{item2}", $"{itemsName2}/{item1}", $"{itemsName2}/{item3}"]]
            );
            // 読み込み可能な状態にしておく
            reader.ReadChunk();

            var xmlToBinary = new StubXmlToBinary();
            var xmlTraverser = new XmlTraverser(
                new StubXPathResolver(),
                xmlToBinary,
                (string path) => new XDocument(),
                (string type, string name, IXmlToBinary xmlToBinary) => new StringWriter(new MemoryStream(), xmlToBinary),
                []
            );

            var writer = new StubDataWriter(xmlToBinary);

            xmlTraverser.Traversal(writer, (reader, xmlTree), []).ToArray();
            // readerにより設定される値の比較
            Assert.IsTrue(reader.Included(writer));
        }

        [TestMethod]
        public void ファイルの結合()
        {
            var itemsName1 = "items1";
            var name1 = "name1";
            var name2 = "name2";
            var name3 = "name3";
            var item1 = "あ";
            var item2 = "い";
            var item3 = "う";
            // データの構築のためのXMLの定義
            var xmlTree = new XDocument(
                new XElement("format",
                    new XElement(
                        "item",
                        new XAttribute("name", name1)
                    ),
                    // 結合されるファイルに関するループ
                    new XElement(
                        "repeat",
                        new XAttribute("type", "combined-xml"),
                        new XAttribute("name", itemsName1),
                        // 結合されるファイルのレコードに関するループ
                        new XElement(
                            "repeat",
                            new XAttribute("type", "combined-record"),
                            // スタブの都合上、nameはそれぞれ独立させておく
                            new XElement(
                                "item",
                                new XAttribute("name", name1)
                            ),
                            new XElement(
                                "item",
                                new XAttribute("type", "combined"),
                                new XAttribute("name", name2)
                            ),
                            new XElement(
                                "item",
                                new XAttribute("name", name3)
                            )
                        )
                    ),
                    new XElement(
                        "item",
                        new XAttribute("name", name2)
                    )
                )
            );
            // XMLにディスパッチするデータの定義
            var reader1 = new StubDataReader(
                [name1, name2, $"{itemsName1}/{name1}", $"{itemsName1}/{name3}"],
                [[item1, item3, item1, item2]]
            );
            var reader2 = new StubDataReader(
                [$"{itemsName1}/{name2}"],
                [[item1], [item2], [item3]]
            );
            var reader3 = new StubDataReader(
                [$"{itemsName1}/{name2}"],
                [[item2], [item3], [item1]]
            );
            var reader4 = new StubDataReader(
                [$"{itemsName1}/{name2}"],
                [[item3], [item1], [item2]]
            );
            // 読み込み可能な状態にしておく
            reader1.ReadChunk();

            var xmlToBinary = new StubXmlToBinary();
            var xmlTraverser = new XmlTraverser(
                new StubXPathResolver(),
                xmlToBinary,
                (string path) => new XDocument(),
                (string type, string name, IXmlToBinary xmlToBinary) => new StringWriter(new MemoryStream(), xmlToBinary),
                []
            );

            using var writer = new StubDataWriter(xmlToBinary);

            xmlTraverser.Traversal(writer, (reader1, xmlTree), [(reader2, new XDocument()), (reader3, new XDocument()), (reader4, new XDocument())]).ToArray();
            // readerにより設定される値の比較
            Assert.IsTrue(reader1.Included(writer, 0, 1));
            Assert.IsTrue(reader2.Included(writer, 0, 3));
            Assert.IsTrue(reader1.Included(writer, 3, 1));
            Assert.IsTrue(reader3.Included(writer, 3, 3));
            Assert.IsTrue(reader1.Included(writer, 6, 1));
            Assert.IsTrue(reader4.Included(writer, 6, 3));
        }

        [TestMethod]
        public void 回数指定のループ()
        {
            var itemsName1 = "items1";
            var name1 = "name1";
            var name2 = "name2";
            var name3 = "name3";
            var item1 = "あ";
            var item2 = "い";
            var item3 = "う";
            var maxCount = 10;
            // データの構築のためのXMLの定義
            var xmlTree = new XDocument(
                new XElement("format",
                    new XElement(
                        "item",
                        new XAttribute("name", name1)
                    ),
                    new XElement(
                        "repeat",
                        new XAttribute("max", maxCount),
                        new XAttribute("name", itemsName1),
                        // スタブの都合上、nameはそれぞれ独立させておく
                        new XElement(
                            "item",
                            new XAttribute("name", name1)
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name2)
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name3)
                        )
                    ),
                    new XElement(
                        "item",
                        new XAttribute("name", name2)
                    )
                )
            );
            // XMLにディスパッチするデータの定義
            var reader = new StubDataReader(
                [name1, name2, $"{itemsName1}/{name1}", $"{itemsName1}/{name2}", $"{itemsName1}/{name3}"],
                [[item1, item2, item1, item2, item3]]
            );
            // 読み込み可能な状態にしておく
            reader.ReadChunk();

            var xmlToBinary = new StubXmlToBinary();
            var xmlTraverser = new XmlTraverser(
                new StubXPathResolver(),
                xmlToBinary,
                (string path) => new XDocument(),
                (string type, string name, IXmlToBinary xmlToBinary) => new StringWriter(new MemoryStream(), xmlToBinary),
                []
            );

            using var writer = new StubDataWriter(xmlToBinary);

            xmlTraverser.Traversal(writer, (reader, xmlTree), []).ToArray();
            // readerにより設定される値の比較
            for (int i = 0; i < maxCount; ++i)
            {
                Assert.IsTrue(reader.Included(writer, i, 1));
                // repeatにより設定されない値の比較
                if (i == 0)
                {
                    Assert.AreEqual(writer.DataArray[i][0], item1);
                    Assert.AreEqual(writer.DataArray[i][4], item2);
                }
                else
                {
                    Assert.AreEqual(writer.DataArray[i][0], "");
                    Assert.AreEqual(writer.DataArray[i].Count, 4);
                }
            }
        }

        [TestMethod]
        public void レコード読み込みのループ()
        {
            var itemsName1 = "items1";
            var name1 = "name1";
            var name2 = "name2";
            var name3 = "name3";
            var item1 = "あ";
            var item2 = "い";
            var item3 = "う";
            // データの構築のためのXMLの定義
            var xmlTree = new XDocument(
                new XElement("format",
                    new XElement(
                        "item",
                        new XAttribute("name", name1)
                    ),
                    new XElement(
                        "repeat",
                        new XAttribute("fetch", "true"),
                        new XAttribute("name", itemsName1),
                        // スタブの都合上、nameはそれぞれ独立させておく
                        new XElement(
                            "item",
                            new XAttribute("name", name1)
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name2)
                        ),
                        new XElement(
                            "item",
                            new XAttribute("name", name3)
                        )
                    ),
                    new XElement(
                        "item",
                        new XAttribute("name", name2)
                    )
                )
            );
            // XMLにディスパッチするデータの定義
            var reader = new StubDataReader(
                [name1, name2, $"{itemsName1}/{name1}", $"{itemsName1}/{name2}", $"{itemsName1}/{name3}"],
                [
                    [item1, item2, item1, item2, item3],
                    ["", "", item1, item2, item3],
                    ["", "", item3, item1, item2],
                    ["", "", item2, item3, item1]
                ]
            );
            // 読み込み可能な状態にしておく
            reader.ReadChunk();

            var xmlToBinary = new StubXmlToBinary();
            var xmlTraverser = new XmlTraverser(
                new StubXPathResolver(),
                xmlToBinary,
                (string path) => new XDocument(),
                (string type, string name, IXmlToBinary xmlToBinary) => new StringWriter(new MemoryStream(), xmlToBinary),
                []
            );

            using var writer = new StubDataWriter(xmlToBinary);

            xmlTraverser.Traversal(writer, (reader, xmlTree), []).ToArray();
            // readerにより設定される値の比較
            Assert.IsTrue(reader.Included(writer));
            for (int i = 0; i < reader.RowCount; ++i)
            {
                // repeatにより設定されない値の比較
                if (i == 0)
                {
                    Assert.AreEqual(writer.DataArray[i][0], item1);
                    Assert.AreEqual(writer.DataArray[i][4], item2);
                }
                else
                {
                    Assert.AreEqual(writer.DataArray[i][0], "");
                    Assert.AreEqual(writer.DataArray[i].Count, 4);
                }
            }
        }
    }
}
