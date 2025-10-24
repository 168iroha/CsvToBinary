namespace CsvToBinary.BuiltIn
{
    /// <summary>
    /// 複数のパターンの文字列の変換の管理のためのクラス
    /// </summary>
    /// <param name="transformerFunc">ファイルを読み込むためのデリゲート</param>
    public class TransformerControl(Func<string, ITransformer> transformerFunc) : ITransformerControl
    {
        /// <summary>
        /// 変換器に関するマップ
        /// </summary>
        private readonly Dictionary<string, ITransformer> transformerDict = [];

        /// <summary>
        /// 変換器を読み込むための関数
        /// </summary>
        private readonly Func<string, ITransformer> transformerFunc = transformerFunc;

        /// <summary>
        /// 文字列を変換をするための変換器の取得
        /// </summary>
        /// <param name="name">変換器の名称</param>
        /// <returns>変換結果の文字列</returns>
        public ITransformer Get(string name)
        {
            // 読み込みにおけるパスの取得
            // 相対パスなら実行ファイルの位置を基準としてパスを構築して読み込む
            var path = Path.IsPathRooted(name) ? name : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            if (this.transformerDict.TryGetValue(path, out ITransformer? value))
            {
                return value;
            }
            else
            {
                var result = this.transformerFunc(path);
                this.transformerDict.Add(path, result);
                return result;
            }
        }
    }
}
