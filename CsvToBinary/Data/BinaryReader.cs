using CsvToBinary.BuiltIn;
using CsvToBinary.Xml;
using System.Numerics;
using System.Xml.Linq;

namespace CsvToBinary.Data
{
    /// <summary>
    /// バイナリデータの読み込みに関するクラス(XmlToBinaryによる書き込みの逆変換を行う)
    /// </summary>
    /// <param name="stream">読み込み元のStream</param>
    /// <param name="xPathResolver">XPathの解決のためのインスタンス</param>
    /// <param name="transformerControl">transformの逆変換のためのインスタンス</param>
    /// <param name="externalDic">変換時に外部から与えるパラメータ</param>
    public class BinaryReader(Stream stream, IXPathResolver xPathResolver, ITransformerControl transformerControl, Dictionary<string, string> externalDic) : IDataReader
    {
        /// <summary>
        /// transformの逆変換のためのインスタンス
        /// </summary>
        private readonly ITransformerControl transformerControl = transformerControl;

        /// <summary>
        /// 読み込み元のStream
        /// </summary>
        private readonly Stream stream = stream;

        /// <summary>
        /// XPathの解決のためのインスタンス
        /// </summary>
        private readonly IXPathResolver xPathResolver = xPathResolver;

        /// <summary>
        /// 変換時に外部から与えるパラメータ
        /// </summary>
        private readonly Dictionary<string, string> externalDic = externalDic;

        /// <summary>
        /// 最後にReadChunkを呼び出した際の戻り値
        /// </summary>
        private bool valid = false;

        /// <summary>
        /// 読み込み対象のデータが残っているかを確認する(データ自体はGetDataで読み進める)
        /// </summary>
        /// <returns>読み込み対象のデータが残っているときにtrue</returns>
        public bool ReadChunk()
        {
            this.valid = this.stream.Position < this.stream.Length;
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
        /// <returns>現在の読み込み位置</returns>
        public int GetChunkId()
        {
            return (int)this.stream.Position;
        }

        /// <summary>
        /// 読み込んだChunkをスタックにPushする
        /// </summary>
        public void Push() { }

        /// <summary>
        /// 読み込んだChunkをスタックからPopする
        /// </summary>
        public void Pop() { }

        /// <summary>
        /// XMLノードから文字列へに変換する(XmlToBinary.ValueNodeEvaluateのサブセット)
        /// </summary>
        /// <param name="element">変換対象のXMLのノード</param>
        /// <param name="parent">elementの親ノード</param>
        /// <returns>変換結果の文字列</returns>
        private string ValueNodeEvaluate(XElement element, XElement parent)
        {
            var str = element.Value;

            return element.Attribute("type")?.Value switch
            {
                null => str,
                // parentを起点としたXPathの評価
                "xpath" => this.xPathResolver.XPathEvaluate(parent, str),
                "external" => this.externalDic.TryGetValue(str, out string? value) ? value : "",
                // counterなど副作用のある評価はここでは実施せず評価不可として扱う
                _ => ""
            };
        }

        /// <summary>
        /// 出力バイト数の取得
        /// </summary>
        /// <param name="item">出力バイト数が記載されたノード</param>
        /// <returns>出力バイト数(指定がないときはnull)</returns>
        private int? GetBytes(XElement item)
        {
            var bytesStr = item.Attribute("bytes")?.Value;
            if (bytesStr is null)
            {
                // bytesの指定がないときはxbytesのXPathを評価して出力バイト数を得る
                var xbyteStr = item.Attribute("xbytes")?.Value;
                if (xbyteStr is not null)
                {
                    bytesStr = this.xPathResolver.XPathEvaluate(item, xbyteStr);
                }
            }
            return bytesStr is null ? null : Int32.Parse(bytesStr);
        }

        /// <summary>
        /// パディング部分を取り除く
        /// </summary>
        /// <param name="buffer">パディングを含むバイト列</param>
        /// <param name="pattern">パディングのバイト列(繰り返し並べられ、領域に収まらない分は切り詰められる)</param>
        /// <param name="left">パディングが本体の前にあるときtrue</param>
        /// <param name="unit">エンコーディングの最小単位のバイト数</param>
        /// <returns>パディングを取り除いたバイト列</returns>
        private static byte[] StripPadding(byte[] buffer, byte[] pattern, bool left, int unit)
        {
            if (left)
            {
                int k = 0;
                while (k < buffer.Length && buffer[k] == pattern[k % pattern.Length])
                {
                    ++k;
                }
                // すべての桁をパディングとして除去されないようにする
                if (k == buffer.Length)
                {
                    k -= unit;
                }
                return [.. buffer.Skip(k - k % unit)];
            }

            // パディング領域の先頭を末尾まで一致する最小の位置として探す
            for (int start = 0; start < buffer.Length; start += unit)
            {
                if (Enumerable.Range(start, buffer.Length - start).All(i => buffer[i] == pattern[(i - start) % pattern.Length]))
                {
                    // すべての桁をパディングとして除去されないようにする
                    if (start == 0)
                    {
                        start = unit;
                    }
                    return [.. buffer.Take(start)];
                }
            }
            return buffer;
        }

        /// <summary>
        /// バイト列を文字列へ変換する
        /// </summary>
        /// <param name="item">変換対象のノード</param>
        /// <param name="buffer">変換対象のバイト列</param>
        /// <returns>変換結果の文字列</returns>
        private static string BytesToString(XElement item, byte[] buffer)
        {
            var encoding = item.Attribute("encoding")?.Value;
            switch (encoding)
            {
                case "binary":
                    return string.Concat(buffer.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
                case "hexadecimal":
                    return BitConverter.ToString(buffer).Replace("-", "");
                case "decimal":
                    // 符号付きのリトルエンディアンとして扱う
                    return new BigInteger(buffer).ToString();
                default:
                    // utf-8などのエンコーディングの場合はパディングを除去してから文字列へ変換する
                    var charaEncoding = CharaTransformer.GetEncoding(encoding);
                    var (direction, padding) = XmlToBinary.GetPadding(item);
                    var pattern = padding.Length > 0 ? charaEncoding.GetBytes(padding) : [XmlToBinary.DefaultPadding];
                    var unit = charaEncoding.GetBytes("a").Length;
                    // 末尾のnullを除去した結果の文字列を返す
                    return charaEncoding.GetString(StripPadding(buffer, pattern, direction == XmlToBinary.PaddingDirection.Left, unit)).TrimEnd('\0');
            }
        }

        /// <summary>
        /// transformによる変換を逆変換する(変換は宣言順に適用されるため逆順に逆変換する)
        /// </summary>
        /// <param name="item">逆変換対象のノード</param>
        /// <param name="value">変換結果の文字列</param>
        /// <returns>逆変換結果の文字列</returns>
        private string InverseTransform(XElement item, string value)
        {
            var transforms = item.Elements().Where(e => e.Name.LocalName == "transform").Reverse().ToList();
            // 空文字列は変換の対象外
            if (transforms.Count == 0 || value.Length == 0)
            {
                return value;
            }
            foreach (var transform in transforms)
            {
                var transformer = this.transformerControl.Get(transform.Value);
                try
                {
                    value = transformer.InverseTransform(value);
                }
                catch (TransformException ex)
                {
                    throw new InputDataException($"変換器[{transform.Value}]による逆変換に失敗しました: {ex.Message}", item, ex);
                }
            }
            return value;
        }

        /// <summary>
        /// XMLに従ったデータの取得方法でデータを得る
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">取得対象のデータの記述子</param>
        /// <returns>取得したデータ</returns>
        public string GetData(string key, XElement item)
        {
            // writer要素内の項目は出力先の決定のためのものでありバイナリには対応しないためbytesが0の場合と同様に扱う
            var bytes = item.Ancestors().Any(e => e.Name.LocalName == "writer") ? 0 : this.GetBytes(item) ?? throw new InputDataException("バイト数が不定の項目からバイナリデータを読み取ることはできません", item);
            if (bytes < 0)
            {
                throw new InputDataException("bytesに負数を設定することはできません", item);
            }

            string strValue = "";
            if (bytes == 0)
            {
                // バイト数が0であってもdefault-valueなどが存在する場合はこれを評価する(XmlToBinary.GetStringのサブセット)
                foreach (XElement child in item.Elements())
                {
                    // 名前空間は無視してタグ名で処理
                    switch (child.Name.LocalName)
                    {
                        case "value":
                            strValue = this.ValueNodeEvaluate(child, item);
                            break;
                        case "default-value":
                            // valueの指定がないときに利用
                            if (strValue.Length == 0)
                            {
                                strValue = this.ValueNodeEvaluate(child, item);
                            }
                            break;
                    }
                }
            }

            if (strValue.Length == 0)
            {
                // バイナリの読み込み
                var buffer = new byte[bytes];
                int total = 0;
                while (total < bytes)
                {
                    int read = this.stream.Read(buffer, total, bytes - total);
                    if (read == 0)
                    {
                        throw new InputDataException("バイナリデータの長さが不足しています", item);
                    }
                    total += read;
                }

                // 逆変換の結果を後続のXPathから参照できるようにresult属性に設定する
                strValue = this.InverseTransform(item, BytesToString(item, buffer));
            }

            item.SetAttributeValue("result", strValue);
            item.SetAttributeValue("result-bytes", bytes);
            return strValue;
        }
    }
}
