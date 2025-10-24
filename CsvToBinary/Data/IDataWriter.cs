using System.Xml.Linq;

namespace CsvToBinary.Data
{
    /// <summary>
    /// データの書き込みに関するインターフェース
    /// </summary>
    public interface IDataWriter : IDisposable
    {
        /// <summary>
        /// 処理単位のデータを書き込む
        /// </summary>
        public void WriteChunk();

        /// <summary>
        /// 書き込んだChunkをスタックにPushする
        /// </summary>
        public void Push();

        /// <summary>
        /// 書き込んだChunkをスタックからPopする
        /// </summary>
        public void Pop();

        /// <summary>
        /// Chunkに関するスタックの深さの取得
        /// </summary>
        public int Depth();

        /// <summary>
        /// XMLに従ったデータの設定方法でデータを設定する
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">設定対象のデータの記述子</param>
        /// <returns>取得したデータ</returns>
        public void SetData(string key, XElement item);
    }
}
