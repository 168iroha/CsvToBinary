using System.Globalization;
using System.Xml.Linq;

namespace CsvToBinary.Data
{
    /// <summary>
    /// CSVファイルの読み込みを行うクラス
    /// </summary>
    public class CsvReader : IDataReader, IDisposable
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
        /// CSVファイルを解析するためのパーサ
        /// </summary>
        public readonly CsvHelper.CsvParser parser;

        /// <summary>
        /// 最後にReadChunkを呼び出した際の戻り値
        /// </summary>
        public bool valid = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="reader">読み込むCSVデータ</param>
        public CsvReader(TextReader reader)
        {
            this.parser = new CsvHelper.CsvParser(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
            {
                // ヘッダは自前で解析するためなしとして扱う
                HasHeaderRecord = false
            });
            // 可能ならヘッダ行の解析
            if (this.parser.Read())
            {
                try
                {
                    // CsvHelper.CsvParser.Recordの評価までCSVの評価は遅延されるためこのタイミングで例外を補足
                    for (int i = 0; i < this.parser.Count; ++i)
                    {
                        this.columnMap[this.parser.Record[i]] = i;
                    }
                }
                catch (CsvHelper.BadDataException ex)
                {
                    throw new BadDataException(
                        "CSVのヘッダの読み込みで異常が発生しました",
                        ex.Context.Parser.Row,
                        ex.Context.Parser.RawRow,
                        ex.Field.Length,
                        ex.RawRecord
                        );
                }
            }
            // 空のカレントデータのセット
            this.stack.Push([]);
        }

        /// <summary>
        /// CSVとしての行の読み込み
        /// </summary>
        /// <returns>行を読み込んだ場合にtrue</returns>
        public bool ReadChunk()
        {
            this.valid = this.parser.Read();
            if (this.valid)
            {
                try
                {
                    // CsvHelper.CsvParser.Recordの評価までCSVの評価は遅延されるためこのタイミングで例外を補足
                    var row = this.parser.Record;
                    this.stack.Pop();
                    this.stack.Push(row);
                }
                catch (CsvHelper.BadDataException ex)
                {
                    throw new BadDataException(
                        "CSVのデータの読み込みで異常が発生しました",
                        ex.Context.Parser.Row,
                        ex.Context.Parser.RawRow,
                        ex.Field.Length,
                        ex.RawRecord
                        );
                }
            }
            return this.valid;
        }

        /// <summary>
        /// 最後にReadChunkを呼び出した際の戻り値の取得
        /// </summary>
        /// <returns>最後にReadChunkを呼び出した際の戻り値(未呼び出しの場合はfalse)</returns>
        public bool Valid()
        {
            return this.valid;
        }

        /// <summary>
        /// 現在読み込んでいる処理単位に対して割り振られるIDの取得
        /// </summary>
        /// <returns></returns>
        public int GetChunkId()
        {
            return this.parser.RawRow;
        }

        /// <summary>
        /// 現在のCSVの読み込み状態のコンテキストをPushする
        /// </summary>
        public void Push()
        {
            this.stack.Push([]);
        }

        /// <summary>
        /// 現在のCSVの読み込み状態のコンテキストをPopする
        /// </summary>
        /// <returns>Popする前のPeekの要素</returns>
        public void Pop()
        {
            this.stack.Pop();
        }

        /// <summary>
        /// ヘッダ名から読み込んでいるCSVの値を取得する
        /// </summary>
        /// <param name="key">ヘッダ名を示すキー</param>
        /// <returns>keyに対応するCSVの値</returns>
        public string this[string key]
        {
            get
            {
                if (this.columnMap.TryGetValue(key, out int index) && this.stack.TryPeek(out string[]? peek) && index < peek.Length)
                {
                    return peek[index];
                }
                return "";
            }
        }

        /// <summary>
        /// XMLに従ったデータの取得方法でデータを得る
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">取得対象のデータの記述子</param>
        /// <returns>取得したデータ</returns>
        public string GetData(string key, XElement item)
        {
            return this[key];
        }

        public void Dispose()
        {
            this.parser.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
