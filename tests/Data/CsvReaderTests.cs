using CsvToBinary.Data;

namespace tests.Data
{
    [TestClass]
    public class CsvReaderTests
    {
        [TestMethod]
        public void 単一行CSVの解析()
        {
            // 検査対象の値
            var header1 = "header1";
            var header2 = "header2";
            var header3 = "header3";
            var notUseHeader = "not-use-header";
            var item1 = "aaa";
            var item2 = "bbb";
            var item3 = "ccc";
            string csvText =
                $"\"{header1}\",{header2},{header3}\n" +
                $"{item1},\"{item2}\",\"{item3}\"";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            // データ部読み込み前は空
            Assert.AreEqual(csv[header1], "");
            Assert.AreEqual(csv[header2], "");
            Assert.AreEqual(csv[header3], "");

            // データ部の1行目の読み込み
            Assert.IsTrue(csv.ReadChunk());
            Assert.IsTrue(csv.Valid());
            // ヘッダ名を指定して要素へアクセス
            Assert.AreEqual(csv[header1], item1);
            Assert.AreEqual(csv[header2], item2);
            Assert.AreEqual(csv[header3], item3);
            // 使用していないヘッダの要素への取得
            Assert.AreEqual(csv[notUseHeader], "");
            // データ部の存在しない2行目の読み込み
            Assert.IsFalse(csv.ReadChunk());
            Assert.IsFalse(csv.Valid());
            // カレントデータは前回読み込んだデータ
            Assert.AreEqual(csv[header1], item1);
            Assert.AreEqual(csv[header2], item2);
            Assert.AreEqual(csv[header3], item3);
        }

        [TestMethod]
        public void 項目のエスケープ付き単一行CSVの解析()
        {
            // 検査対象の値
            var header1 = "\nheader\n\"1\"\n";
            var item1 = "\naaa\"bbb,\nccc\n";
            string csvText =
                $"\"{header1.Replace("\"", "\"\"")}\"\n" +
                $"\"{item1.Replace("\"", "\"\"")}\"";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            // データ部の1行目の読み込み
            Assert.IsTrue(csv.ReadChunk());
            Assert.IsTrue(csv.Valid());
            // ヘッダ名を指定して要素へアクセス
            Assert.AreEqual(csv[header1], item1);
            // データ部の存在しない2行目の読み込み
            Assert.IsFalse(csv.ReadChunk());
            Assert.IsFalse(csv.Valid());
        }

        [TestMethod]
        public void カラムの増減の許容の確認()
        {
            // 検査対象の値
            var header1 = "header1";
            var header2 = "header2";
            var item1_1 = "aaa1";
            var item2_1 = "aaa2";
            var item2_2 = "bbb2";
            var item2_3 = "ccc2";
            string csvText =
                $"{header1},{header2}\n" +
                $"{item1_1}\n" +
                $"{item2_1},{item2_2},{item2_3}\n";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            // データ部の不足した1行目の読み込み
            Assert.IsTrue(csv.ReadChunk());
            Assert.IsTrue(csv.Valid());
            Assert.AreEqual(csv[header1], item1_1);
            Assert.AreEqual(csv[header2], "");

            // データ部の過剰な2行目の読み込み
            Assert.IsTrue(csv.ReadChunk());
            Assert.IsTrue(csv.Valid());
            Assert.AreEqual(csv[header1], item2_1);
            Assert.AreEqual(csv[header2], item2_2);
        }

        [TestMethod]
        public void データ部が空のCSVの解析()
        {
            // 検査対象の値
            var header1 = "header1";
            var header2 = "header2";
            var header3 = "header3";
            string csvText =
                $"\"{header1}\",{header2},{header3}\n";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            // データ部の1行目の読み込み
            Assert.IsFalse(csv.ReadChunk());
            Assert.IsFalse(csv.Valid());
        }

        [TestMethod]
        public void 空のCSVの解析()
        {
            // 検査対象の値
            string csvText = "";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            // データ部の1行目の読み込み
            Assert.IsFalse(csv.ReadChunk());
            Assert.IsFalse(csv.Valid());
        }

        [TestMethod]
        public void 解析の状態のスタックの確認()
        {
            // 検査対象の値
            var header1 = "header1";
            var header2 = "header2";
            var header3 = "header3";
            var item1_1 = "aaa1";
            var item1_2 = "bbb1";
            var item1_3 = "ccc1";
            var item2_1 = "aaa2";
            var item2_2 = "bbb2";
            var item2_3 = "ccc2";
            var item3_1 = "aaa3";
            var item3_2 = "bbb3";
            var item3_3 = "ccc3";
            string csvText =
                $"{header1},{header2},{header3}\n" +
                $"{item1_1},{item1_2},{item1_3}\n" +
                $"{item2_1},{item2_2},{item2_3}\n" +
                $"{item3_1},{item3_2},{item3_3}\n";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            // データ部の1行目の読み込み
            Assert.IsTrue(csv.ReadChunk());
            Assert.IsTrue(csv.Valid());

            // Push前
            Assert.AreEqual(csv[header1], item1_1);
            Assert.AreEqual(csv[header2], item1_2);
            Assert.AreEqual(csv[header3], item1_3);
            {
                csv.Push();
                // Push直後は空
                Assert.AreEqual(csv[header1], "");
                Assert.AreEqual(csv[header2], "");
                Assert.AreEqual(csv[header3], "");

                // データ部の2行目の読み込み
                Assert.IsTrue(csv.ReadChunk());
                Assert.IsTrue(csv.Valid());
                Assert.AreEqual(csv[header1], item2_1);
                Assert.AreEqual(csv[header2], item2_2);
                Assert.AreEqual(csv[header3], item2_3);

                {
                    csv.Push();
                    // Push直後は空
                    Assert.AreEqual(csv[header1], "");
                    Assert.AreEqual(csv[header2], "");
                    Assert.AreEqual(csv[header3], "");

                    // データ部の3行目の読み込み
                    Assert.IsTrue(csv.ReadChunk());
                    Assert.IsTrue(csv.Valid());
                    Assert.AreEqual(csv[header1], item3_1);
                    Assert.AreEqual(csv[header2], item3_2);
                    Assert.AreEqual(csv[header3], item3_3);

                    csv.Pop();
                }

                // Pushした2行目が復元される
                Assert.AreEqual(csv[header1], item2_1);
                Assert.AreEqual(csv[header2], item2_2);
                Assert.AreEqual(csv[header3], item2_3);

                csv.Pop();
            }

            // Pushした1行目が復元される
            Assert.AreEqual(csv[header1], item1_1);
            Assert.AreEqual(csv[header2], item1_2);
            Assert.AreEqual(csv[header3], item1_3);
        }

        [TestMethod]
        public void フォーマットが異常なCSVの解析()
        {
            // 検査対象の値
            var header1 = "header1";
            var header2 = "header2";
            var header3 = "header3";
            var item1_1 = "aaa1";
            var item1_2 = "bbb1";
            var item1_3 = "ccc1";
            var item2_1 = "aaaaaa\"bbb2";
            var item2_2 = "bbb2";
            var item2_3 = "ccc2";
            var rowText = $"{item2_1},{item2_2},{item2_3}";
            int row = 3;
            string csvText =
                $"{header1},{header2},{header3}\n" +
                $"{item1_1},{item1_2},{item1_3}\n" +
                $"{rowText}";

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader);

            try
            {
                Assert.IsTrue(csv.ReadChunk());
                Assert.IsTrue(csv.Valid());
                Assert.IsFalse(csv.ReadChunk());
                Assert.Fail();
            }
            catch (BadDataException ex)
            {
                // 異常が発生した場所の検査
                Assert.AreEqual(ex.Row, row);
                Assert.AreEqual(ex.RawRow, row);
                Assert.AreEqual(ex.RowRecord, rowText);

                // この値については利用するパーサによって変化する
                Assert.AreEqual(ex.RowPos, rowText.IndexOf(','));

                // ロールバックの確認
                Assert.AreEqual(csv[header1], item1_1);
                Assert.AreEqual(csv[header2], item1_2);
                Assert.AreEqual(csv[header3], item1_3);
            }
        }
    }
}