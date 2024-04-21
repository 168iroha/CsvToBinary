using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace BuiltIn
{
    /// <summary>
    /// 単純文字変換のためのクラス
    /// </summary>
    public class CharaTransformer : ITransformer
    {
        /// <summary>
        /// 1つの単純文字変換結果の取得のためのインターフェース
        /// </summary>
        private interface ICharaMap
        {
            /// <summary>
            /// 変換結果の文字列の取得
            /// </summary>
            /// <returns>変換結果の文字列</returns>
            public string Get();
        }

        /// <summary>
        /// 1つの単純文字変換結果の取得のためのクラス
        /// </summary>
        private readonly struct CharaMap : ICharaMap
        {
            /// <summary>
            /// 変換結果の文字列
            /// </summary>
            private readonly string to;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="to">変換結果の文字列</param>
            public CharaMap(string to)
            {
                this.to = to;
            }

            /// <summary>
            /// 変換結果の文字列の取得
            /// </summary>
            /// <returns>変換結果の文字列</returns>
            public readonly string Get()
            {
                return this.to;
            }
        }

        /// <summary>
        /// 1つのファイルからの単純文字変換結果の取得のためのクラス
        /// </summary>
        private readonly struct CharaMapFromFile : ICharaMap
        {
            /// <summary>
            /// 変換先の文字列を示すパス(相対パスなら実行ファイルの位置を基準としてパスを構築)
            /// </summary>
            private readonly string to;
            /// <summary>
            /// 親クラスのインスタンス
            /// </summary>
            private readonly CharaTransformer inst;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="to">変換先の文字列を示すパス</param>
            /// <param name="inst">親クラスのインスタンス</param>
            public CharaMapFromFile(string to, CharaTransformer inst)
            {
                this.to = Path.IsPathRooted(to) ? to : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, to);
                this.inst = inst;
            }

            /// <summary>
            /// 変換結果の文字列の取得
            /// </summary>
            /// <returns>変換結果の文字列</returns>
            public readonly string Get()
            {
                return this.inst.LoadFile(this.to);
            }
        }

        /// <summary>
        /// 1つの正規表現を基にした単純文字変換結果の取得のためのインターフェース
        /// </summary>
        private interface IRegexCharaMap
        {
            /// <summary>
            /// 変換結果の文字列の取得
            /// </summary>
            /// <param name="from">変換元の文字列</param>
            /// <param name="offset">fromのオフセット</param>
            /// <returns>変換が実施されたか</returns>
            public bool TryGet(string from, ref int offset, [MaybeNullWhen(false)] out string to);
        }

        /// <summary>
        /// 1つのファイルからの正規表現を基にした単純文字変換結果の取得のためのクラス
        /// </summary>
        private readonly struct RegexCharaMap : IRegexCharaMap
        {
            /// <summary>
            /// 変換元のパターンマッチングを行う正規表現
            /// </summary>
            private readonly Regex regex;
            /// <summary>
            /// 正規表現の結果の埋め込みが可能な変換結果の文字列
            /// </summary>
            private readonly string to;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="regex">変換元のパターンマッチングを行う正規表現</param>
            /// <param name="to">正規表現の結果の埋め込みが可能な変換結果の文字列</param>
            public RegexCharaMap(Regex regex, string to)
            {
                this.regex = regex;
                this.to = to;
            }

            /// <summary>
            /// 変換結果の文字列の取得
            /// </summary>
            /// <param name="from">変換元の文字列</param>
            /// <param name="offset">fromのオフセット</param>
            /// <returns>変換が実施されたか</returns>
            public readonly bool TryGet(string from, ref int offset, [MaybeNullWhen(false)] out string to)
            {
                var m = this.regex.Match(from, offset);
                if (m.Success)
                {
                    offset += m.Length;
                    to = string.Format(this.to, m.Groups.Values.ToArray());
                    return true;
                }
                to = null;
                return false;
            }
        }

        /// <summary>
        /// 1つの正規表現を基にした単純文字変換結果の取得のためのクラス
        /// </summary>
        private readonly struct RegexCharaMapFromFile : IRegexCharaMap
        {
            /// <summary>
            /// 変換元のパターンマッチングを行う正規表現
            /// </summary>
            private readonly Regex regex;
            /// <summary>
            /// 正規表現の結果の埋め込みが可能な変換結果の文字列
            /// </summary>
            private readonly string to;
            /// <summary>
            /// 親クラスのインスタンス
            /// </summary>
            private readonly CharaTransformer inst;

            public RegexCharaMapFromFile(Regex regex, string to, CharaTransformer inst)
            {
                this.regex = regex;
                this.to = to;
                this.inst = inst;
            }

            /// <summary>
            /// 変換結果の文字列の取得
            /// </summary>
            /// <param name="from">変換元の文字列</param>
            /// <param name="offset">fromのオフセット</param>
            /// <returns>変換が実施されたか</returns>
            public readonly bool TryGet(string from, ref int offset, [MaybeNullWhen(false)] out string to)
            {
                var m = this.regex.Match(from, offset);
                if (m.Success)
                {
                    offset += m.Length;
                    // 変換先のパスの構築(相対パスなら実行ファイルの位置を基準としてパスを構築)
                    var toFile = string.Format(this.to, m.Groups.Values.ToArray());
                    var toFile2 = Path.IsPathRooted(toFile) ? toFile : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, toFile);
                    to = this.inst.LoadFile(toFile2);
                    return true;
                }
                to = null;
                return false;
            }
        }

        /// <summary>
        /// 文字列の長さごとの変換規則の定義(Listの若いインデックスに文字列の大きい対象が格納される)
        /// </summary>
        private readonly List<(int, Dictionary<string, ICharaMap>)> longestMap = [];
        /// <summary>
        /// 正規表現による変換規則の定義
        /// </summary>
        private readonly List<IRegexCharaMap> regexMap = [];
        /// <summary>
        /// 変換結果のテキストを読み込むための関数
        /// </summary>
        private readonly Func<string, string> stringFunc;
        /// <summary>
        /// stringFuncの実行結果に関するキャッシュ
        /// </summary>
        private readonly Dictionary<string, string> stringCache = [];


        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="doc">変換規則を定義したXML</param>
        /// <param name="stringFunc">変換結果のテキストを読み込むための関数</param>
        public CharaTransformer(XDocument doc, Func<string, string> stringFunc) {
            var dict = new SortedDictionary<int, Dictionary<string, ICharaMap>>();
            // 変換規則を定義したXMLを読み込んで変換テーブルを構築する
            foreach (XElement element in doc.XPathSelectElements("/transform/map"))
            {
                var toAttr = element.Attribute("to");
                var toFileAttr = element.Attribute("to-file");
                var fromAttr = element.Attribute("from");
                var fromRegexAttr = element.Attribute("from-regex");

                // 属性の正当性の評価
                if (fromAttr is null && fromRegexAttr is null)
                {
                    throw new ArgumentException("CharaTransformerに関する変換元はfromあるいはfrom-regexで指定する必要があります");
                }
                if (toAttr is null && toFileAttr is null)
                {
                    throw new ArgumentException("CharaTransformerに関する変換結果はtoあるいはto-fileで指定する必要があります");
                }

                // 各種変換方式の読み込み
                if (fromAttr is not null)
                {
                    var from = fromAttr.Value;
                    var length = from.Length;
                    if (!dict.TryGetValue(length, out var map))
                    {
                        map = [];
                        dict.Add(length, map);
                    }
                    // 単純な文字列による変換の定義
                    map.Add(
                        from,
                        toAttr is not null ?
                        new CharaMap(toAttr.Value) :
                        new CharaMapFromFile(toFileAttr!.Value, this)
                    );
                }
                else if (fromRegexAttr is not null)
                {
                    var from = fromRegexAttr.Value;
                    // 正規表現による変換の定義(先頭マッチで計算するようにする)
                    var regex = new Regex(@"\G" + from, RegexOptions.Compiled);
                    this.regexMap.Add(
                        toAttr is not null ?
                        new RegexCharaMap(regex, toAttr.Value) :
                        new RegexCharaMapFromFile(regex, toFileAttr!.Value, this)
                    );
                }
            }
            // 変換テーブルへの代入
            foreach (var pair in dict.Reverse())
            {
                this.longestMap.Add((pair.Key, pair.Value));
            }

            this.stringFunc = stringFunc;
        }

        /// <summary>
        /// 変換で利用するテキストファイルの読み込み
        /// </summary>
        /// <param name="path">読み込むファイルパス</param>
        /// <returns>読み込み結果のテキスト</returns>
        private string LoadFile(string path)
        {
            if (!this.stringCache.TryGetValue(path, out var text))
            {
                text = this.stringFunc(path);
                // 読み込み結果をキャッシュしておく
                this.stringCache.Add(path, text);
            }
            return text;
        }

        /// <summary>
        /// 文字列を変換する
        /// </summary>
        /// <param name="from">変換元の文字列</param>
        /// <returns>変換結果の文字列</returns>
        public string Transform(string from)
        {
            var result = "";
            int pos = 0;

            while (pos < from.Length)
            {
                // マッチしたかを判定する変数
                bool match = false;

                // ロンゲストマッチを行って得られた規則から変換を試みる
                foreach (var (length, map) in this.longestMap)
                {
                    if (length <= from.Length - pos)
                    {
                        var sub = from.Substring(pos, length);
                        if (map.TryGetValue(sub, out var value))
                        {
                            pos += length;
                            result += value.Get();
                            match = true;
                            break;
                        }
                    }
                }
                if (!match)
                {
                    // どれにも一致するものがないときは正規表現により変換
                    foreach (var map in this.regexMap)
                    {
                        if (map.TryGet(from, ref pos, out var value))
                        {
                            result += value;
                            match = true;
                            break;
                        }
                    }
                }
                if (!match)
                {
                    // 変換規則が存在しないときは異常
                    throw new TransformException("文字列に関する変換規則が見つかりませんでした", pos, from);
                }
            }

            return result;
        }
    }
}
