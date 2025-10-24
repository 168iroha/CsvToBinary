namespace CsvToBinary.Data
{
    /// <summary>
    /// CSVのデータ異常に関する例外クラス
    /// </summary>
    /// <param name="message">例外のメッセージ</param>
    /// <param name="row">CSVの行番号</param>
    /// <param name="rawRow">セルの改行等を無視したCSVファイルの行番号</param>
    /// <param name="rowPos">異常が発生した行の位置</param>
    /// <param name="rowRecord">該当する行のテキスト</param>
    public class BadDataException(string message, long row, long rawRow, long rowPos, string rowRecord) : Exception(
            $"{message}{Environment.NewLine}" + 
                $"Row: {row}{Environment.NewLine}" + 
                $"RawRow: {rawRow}{Environment.NewLine}" +
                $"RowPos: {rowPos}{Environment.NewLine}" +
                $"RowRecord: {rowRecord}"
            )
    {
        /// <summary>
        /// CSVの行番号
        /// </summary>
        public long Row { get; } = row;
        /// <summary>
        /// セルの改行等を無視したCSVファイルの行番号
        /// </summary>
        public long RawRow { get; } = rawRow;
        /// <summary>
        /// 異常が発生した行の位置
        /// </summary>
        public long RowPos { get; } = rowPos;
        /// <summary>
        /// 該当する行のテキスト
        /// </summary>
        public string RowRecord { get; } = rowRecord;
    }
}
