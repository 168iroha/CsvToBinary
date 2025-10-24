using System.Xml.Linq;

namespace CsvToBinary.Data
{
    /// <summary>
    /// データの読み込みに関するインターフェース
    /// </summary>
    public interface IDataReader
    {
        /// <summary>
        /// 処理単位のデータを読み込む
        /// </summary>
        /// <returns>データを読み込んだ時にtrue</returns>
        public bool ReadChunk();

        /// <summary>
        /// 最後にReadChunkを呼び出した際の戻り値の取得
        /// </summary>
        /// <returns>最後にReadChunkを呼び出した際の戻り値(未呼び出しの場合はfalse)</returns>
        public bool Valid();

        /// <summary>
        /// 現在読み込んでいる処理単位に対して割り振られるIDの取得
        /// </summary>
        /// <returns></returns>
        public int GetChunkId();

        /// <summary>
        /// 読み込んだChunkをスタックにPushする
        /// </summary>
        public void Push();

        /// <summary>
        /// 読み込んだChunkをスタックからPopする
        /// </summary>
        public void Pop();

        /// <summary>
        /// XMLに従ったデータの取得方法でデータを得る
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">取得対象のデータの記述子</param>
        /// <returns>取得したデータ</returns>
        public string GetData(string key, XElement item);
    }
}
