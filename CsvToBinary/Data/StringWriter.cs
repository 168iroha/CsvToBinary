using System.Text;
using System.Xml.Linq;
using CsvToBinary.Xml;

namespace CsvToBinary.Data
{
    /// <summary>
    /// 文字列データの書き込みに関するクラス
    /// </summary>
    /// <param name="stream">書き込み先のStream</param>
    /// <param name="xmlToBinary">XMLをStreamへ書き出す処理が記載されたインスタンス</param>
    public class StringWriter(Stream stream, IXmlToBinary xmlToBinary) : IDataWriter
    {
        /// <summary>
        /// 書き込み先のStream
        /// </summary>
        private readonly Stream stream = stream;

        /// <summary>
        /// ファイルIO制御のためのMutex
        /// </summary>
        private readonly Mutex mutex = new(false);

        /// <summary>
        /// XMLをStreamへ書き出す処理が記載されたインスタンス
        /// </summary>
        private readonly IXmlToBinary xmlToBinary = xmlToBinary;

        /// <summary>
        /// 処理単位のデータを書き込む
        /// </summary>
        /// <param name="cnt">0以上のときはループ終端時の書き込み契機によるループカウント</param>
        public void WriteChunk(int cnt) { }

        /// <summary>
        /// 書き込んだChunkをスタックにPushする
        /// </summary>
        public void Push() { }

        /// <summary>
        /// 書き込んだChunkをスタックからPopする
        /// </summary>
        public void Pop() { }

        /// <summary>
        /// Chunkに関するスタックの深さの取得
        /// </summary>
        public int Depth()
        {
            return 0;
        }

        /// <summary>
        /// XMLに従ったデータの設定方法でデータを設定する
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">設定対象のデータの記述子</param>
        /// <returns>取得したデータ</returns>
        public void SetData(string key, XElement item)
        {
            try
            {
                this.mutex.WaitOne();
                this.xmlToBinary.Write(this.stream, item);
            }
            finally {
                this.mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// 書き込み結果の文字列の取得
        /// </summary>
        /// <returns>書き込み結果の文字列</returns>
        public string GetString()
        {
            try
            {
                this.mutex.WaitOne();
                this.stream.Flush();
                var pos = this.stream.Position;
                this.stream.Position = 0;
                using var sr = new StreamReader(this.stream, Encoding.UTF8);
                var ret = sr.ReadToEnd();
                this.stream.Position = pos;
                return ret;
            }
            finally
            {
                this.mutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            this.stream.Dispose();
            this.mutex.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
