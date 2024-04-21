namespace Data
{
    /// <summary>
    /// CSVのデータ異常に関する例外クラス
    /// </summary>
    public class BadDataException : Exception
    {
        /// <summary>
        /// CSVの行番号
        /// </summary>
        public long Row { get; }
        /// <summary>
        /// セルの改行等を無視したCSVファイルの行番号
        /// </summary>
        public long RawRow { get; }
        /// <summary>
        /// 異常が発生した行の位置
        /// </summary>
        public long RowPos { get; }
        /// <summary>
        /// 該当する行のテキスト
        /// </summary>
        public string RowRecord { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">例外のメッセージ</param>
        /// <param name="row">CSVの行番号</param>
        /// <param name="rawRow">セルの改行等を無視したCSVファイルの行番号</param>
        /// <param name="rowPos">異常が発生した行の位置</param>
        /// <param name="rowRecord">該当する行のテキスト</param>
        public BadDataException(string message, long row, long rawRow, long rowPos, string rowRecord) :
            base(
                $"{message}{Environment.NewLine}" + 
                $"Row: {row}{Environment.NewLine}" + 
                $"RawRow: {rawRow}{Environment.NewLine}" +
                $"RowPos: {rowPos}{Environment.NewLine}" +
                $"RowRecord: {rowRecord}"
            )
        {
            this.Row = row;
            this.RawRow = rawRow;
            this.RowPos = rowPos;
            this.RowRecord = rowRecord;
        }
    }
}
