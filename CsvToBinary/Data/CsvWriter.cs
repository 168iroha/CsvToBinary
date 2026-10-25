using System.Globalization;
using System.Xml.Linq;

namespace CsvToBinary.Data
{
    /// <summary>
    /// CSVの書き込みに関するクラス(テストにおけるStubDataWriterとほぼ同等)
    /// </summary>
    /// <param name="writer">書き込み先のTextWriter</param>
    public class CsvWriter(TextWriter writer) : IDataWriter
    {
        /// <summary>
        /// 書き込み先のTextWriter
        /// </summary>
        private readonly TextWriter writer = writer;

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
        /// Push/Popに対応するスコープが行のスタックに反映済みかのリスト(末尾が最も内側)
        /// </summary>
        private readonly List<bool> realized = [];

        /// <summary>
        /// pushやpopにおけるネストの深さ
        /// </summary>
        private int nestLevel = 0;

        /// <summary>
        /// Disposeが実施済みか
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// XMLに従ったデータの設定方法でデータを設定する
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">設定対象のデータの記述子</param>
        public void SetData(string key, XElement item)
        {
            // バイナリに実体がなくスキップされた項目はCSVにも出力しない
            if (item.Attribute("skipped") is not null)
            {
                return;
            }
            // 出力なし固定の項目はCSVにも出力しない
            if (item.Attribute("bytes")?.Value == "0")
            {
                return;
            }
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
                // 書き込み先のカラムの分の配列を確保していないときは書き込み先の分を追加
                peek.AddRange(Enumerable.Repeat("", index + 1 - peek.Count));
            }
            // エンコーディング等の適用前の生の文字列の書き込み
            peek[index] = item.Element("value")?.Value ?? "";
        }

        /// <summary>
        /// 書き込んだChunkをスタックからPopする
        /// </summary>
        public void Pop()
        {
            // 行のスタックに反映されていないスコープ(import先のTraversalの開始時のPushなど)のPopでは行を破棄しない
            if (this.realized.Count > 0)
            {
                var last = this.realized.Count - 1;
                var wasRealized = this.realized[last];
                this.realized.RemoveAt(last);
                if (!wasRealized)
                {
                    return;
                }
            }

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
            }
            --this.nestLevel;
            this.stack.Pop();
        }

        /// <summary>
        /// 書き込んだChunkをスタックにPushする
        /// </summary>
        public void Push()
        {
            // 書き込み可能な状態にする場合に限り行のスタックへ反映する
            if (this.nestLevel == 0)
            {
                ++this.nestLevel;
                this.stack.Push([]);
                this.realized.Add(true);
            }
            else
            {
                this.realized.Add(false);
            }
        }

        /// <summary>
        /// 処理単位のデータを書き込む
        /// </summary>
        /// <param name="cnt">0以上のときはループ終端時の書き込み契機によるループカウント</param>
        public void WriteChunk(int cnt)
        {
            // 行のスタックに反映されていないスコープの終端では行を出力しない(import先のTraversalの終了時など)
            if (cnt < 0 && this.realized.Count > 0 && !this.realized[^1])
            {
                return;
            }

            if (this.stack.TryPeek(out List<string>? peek))
            {
                List<List<string>> targetArray;
                // 初回のループの終端として扱うのはPushされたが行のスタックに反映されていないスコープが存在するときのみ
                if (cnt != 0 || !this.realized.Contains(false))
                {
                    if (this.nestLevel > 1)
                    {
                        // ネストの深さが深いときは一時的なデータ列へ書き込み
                        var size = this.tempDataArray.Count;
                        if (size < this.nestLevel - 1)
                        {
                            // 書き込み可能だがまだ書き込んでいないデータの配列サイズが不足しているときは拡張
                            this.tempDataArray.AddRange(Enumerable.Range(0, this.nestLevel - 1 - size).Select(_ => new List<List<string>>()));
                        }
                        targetArray = this.tempDataArray[this.nestLevel - 2];
                    }
                    else
                    {
                        // ネストが最低限の場合は直接書き出す
                        targetArray = this.dataArray;
                    }
                    // 何も書き込まれていない行は出力しない(writer要素による書き込み先の切り替え後に生じる)
                    if (peek.Count > 0)
                    {
                        targetArray.Add(peek);
                    }

                    // this.tempDataArrayのより深いところへの書き込みをマージする
                    for (int i = this.nestLevel - 1; i < this.tempDataArray.Count; ++i)
                    {
                        targetArray.AddRange(this.tempDataArray[i]);
                    }
                    if (this.tempDataArray.Count > this.nestLevel - 1)
                    {
                        this.tempDataArray.RemoveRange(this.nestLevel - 1, this.tempDataArray.Count - (this.nestLevel - 1));
                    }

                    // スタックへの書き込み状況をリセットする
                    this.stack.Pop();
                    this.stack.Push([]);
                }
                else
                {
                    // 初回のループ時の書き込みはループ前のものと統合するためこのタイミングでスタックする(ループの開始時のPushに対応する最も内側のスコープを反映する)
                    this.realized[this.realized.LastIndexOf(false)] = true;
                    ++this.nestLevel;
                    this.stack.Push([]);
                }
            }
        }

        /// <summary>
        /// Chunkに関するスタックの深さの取得
        /// </summary>
        public int Depth()
        {
            return this.realized.Count;
        }

        /// <summary>
        /// 書き込み済みのデータをCSVとしてTextWriterへ出力する
        /// </summary>
        private void WriteCsv()
        {
            using var csv = new CsvHelper.CsvWriter(this.writer, CultureInfo.InvariantCulture, true);
            var header = this.columnMap.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();
            foreach (var name in header)
            {
                csv.WriteField(name);
            }
            csv.NextRecord();
            foreach (var row in this.dataArray)
            {
                for (int i = 0; i < header.Count; ++i)
                {
                    csv.WriteField(i < row.Count ? row[i] : "");
                }
                csv.NextRecord();
            }
            csv.Flush();
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                this.WriteCsv();
                this.writer.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
