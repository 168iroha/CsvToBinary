using CsvToBinary.BuiltIn;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace CsvToBinary.Xml
{

    /// <summary>
    /// XMLをバイナリ形式でStreamへ書き込むクラス
    /// </summary>
    /// <param name="transformerControl">transformerによる文字列変換のためのインスタンス</param>
    /// <param name="counter">transformerによる文字列変換のためのインスタンス</param>
    /// <param name="xPathResolver">XPathの解決のためのインスタンス</param>
    /// <param name="externalDic">変換時に外部から与えるパラメータ</param>
    public class XmlToBinary(ITransformerControl transformerControl, ICounter counter, IXPathResolver xPathResolver, Dictionary<string, string> externalDic) : IXmlToBinary
    {
        /// <summary>
        /// パディングの方向の定義
        /// </summary>
        private enum PaddingDirection
        {
            Left, Right
        }

        /// <summary>
        /// デフォルトのパディング
        /// </summary>
        public const byte DefaultPadding = 0;

        /// <summary>
        /// transformerによる文字列変換のためのインスタンス
        /// </summary>
        private readonly ITransformerControl transformerControl = transformerControl;

        /// <summary>
        /// カウンタ値の取得のためのインスタンス
        /// </summary>
        private readonly ICounter counter = counter;

        /// <summary>
        /// XPathの解決のためのインスタンス
        /// </summary>
        private readonly IXPathResolver xPathResolver = xPathResolver;

        /// <summary>
        /// 変換時に外部から与えるパラメータ
        /// </summary>
        private readonly Dictionary<string, string> externalDic = externalDic;

        /// <summary>
        /// 文字列を任意のエンコーディングでバイト列に変換する(エンディアンの変換は未実装)<br />
        /// この関数は引数bytesによる例外は発生しない
        /// </summary>
        /// <param name="str">変換対象の文字列</param>
        /// <param name="encoding">エンコーディング</param>
        /// <param name="bytes">出力バイト数</param>
        /// <returns>変換結果のバイト列</returns>
        static private byte[] StringToBytes(string str, string? encoding, int bytes = 0)
        {
            return encoding switch
            {
                "binary" => ConvertToBytes.FromBinary(str),
                "hexadecimal" => ConvertToBytes.FromHexadecimal(str),
                "decimal" => ConvertToBytes.FromDecimal(str, bytes),
                "utf-8" => Encoding.UTF8.GetBytes(str),
                "utf-16" => Encoding.Unicode.GetBytes(str),
                "utf-16le" => Encoding.Unicode.GetBytes(str),
                "utf-16be" => Encoding.BigEndianUnicode.GetBytes(str),
                "shift-jis" => Encoding.GetEncoding(932).GetBytes(str),
                // デフォルトでUTF-8によるバイナリ列を利用
                _ => Encoding.UTF8.GetBytes(str)
            };
        }

        /// <summary>
        /// 自動採番を行う
        /// </summary>
        /// <param name="element"></param>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string AutoIncrement(XElement element, string str)
        {
            if (str.Length == 0)
            {
                element.Value = "1";
                return "0";
            }
            element.Value = (Int64.Parse(str) + 1).ToString();
            return str;
        }

        /// <summary>
        /// XMLノードから文字列へに変換する
        /// </summary>
        /// <param name="element">変換対象のXMLのノード</param>
        /// <param name="p">elementの親ノード</param>
        /// <returns>変換結果の文字列</returns>
        private string ValueNodeEvaluate(XElement element, XElement parent)
        {
            var str = element.Value;

            return element.Attribute("type")?.Value switch
            {
                // parentを起点としたXPathの評価
                "xpath" => this.xPathResolver.XPathEvaluate(parent, str),
                "current-time" => DateTime.Now.ToString(str, CultureInfo.InvariantCulture),
                "counter" => this.counter.Count(str.Length == 0 ? null : str).ToString(),
                "auto-increment" => AutoIncrement(element, str),
                "external" => this.externalDic.TryGetValue(str, out string? value) ? value : "",
                _ => str
            };
        }

        /// <summary>
        /// パディングをStreamへ書き込む
        /// </summary>
        /// <param name="stream">バイナリの書き込みを行うStream(バッファリングが行われることを前提とする)</param>
        /// <param name="length">パディングを書き込む領域長</param>
        /// <param name="paddingBuffer">パディングを示すバイナリ</param>
        private static void WritePadding(Stream stream, int length, byte[] paddingBuffer)
        {
            var buffer = paddingBuffer.Length > 0 ? paddingBuffer : [DefaultPadding];
            int fullPaddingLength = length / buffer.Length;
            int restPaddingByteNum = length - fullPaddingLength * buffer.Length;
            int i = 0;
            for (; i < fullPaddingLength; ++i)
            {
                stream.Write(buffer);
            }
            if (restPaddingByteNum > 0)
            {
                // パディングが余った部分はパディングを切り詰めて出力
                stream.Write(buffer, 0, restPaddingByteNum);
            }
        }

        /// <summary>
        /// パディングをStreamへ書き込む(パディングはnull)
        /// </summary>
        /// <param name="stream">バイナリの書き込みを行うStream(バッファリングが行われることを前提とする)</param>
        /// <param name="length">パディングを書き込む領域長</param>
        private static void WritePadding(Stream stream, int length)
        {
            // デフォルトではパディングはnullとする
            for (int i = 0; i < length; ++i)
            {
                stream.WriteByte(DefaultPadding);
            }
        }

        /// <summary>
        /// XMLのノードの文字列表現を取得
        /// </summary>
        /// <param name="element">文字列表現取得対象のXMLノード</param>
        /// <returns>elementの文字列表現</returns>
        public string GetString(XElement element)
        {
            // 書き込み対象の文字列としての値
            string strValue = "";

            foreach (XElement child in element.Elements())
            {
                // 名前空間は無視してタグ名で処理
                switch (child.Name.LocalName)
                {
                    case "value":
                        strValue = this.ValueNodeEvaluate(child, element);
                        break;
                    case "default-value":
                        // valueの指定がないときに利用
                        if (strValue.Length == 0)
                        {
                            strValue = this.ValueNodeEvaluate(child, element);
                        }
                        break;
                    case "transform":
                        if (strValue.Length != 0)
                        {
                            // 変換器による変換の適用
                            strValue = this.transformerControl.Get(child.Value).Transform(strValue);
                        }
                        break;
                }
            }

            return strValue;
        }

        /// <summary>
        /// 出力バイト数の取得
        /// </summary>
        /// <param name="element">出力バイト数が記載されたノード</param>
        /// <returns>出力バイト数</returns>
        private int? GetBytes(XElement element)
        {
            // 出力バイト数の検証
            var bytesStr = element.Attribute("bytes")?.Value;
            if (bytesStr is null)
            {
                // bytesの指定がないときはxbytesのXPathを評価して出力バイト数を得る
                var xbyteStr = element.Attribute("xbytes")?.Value;
                if (xbyteStr is not null)
                {
                    bytesStr = this.xPathResolver.XPathEvaluate(element, xbyteStr);
                }
            }
            return bytesStr is null ? null : Int32.Parse(bytesStr);
        }

        /// <summary>
        /// パディング情報の取得
        /// </summary>
        /// <param name="element">パディング情報が記載されたノード</param>
        /// <returns>パディング情報</returns>
        private static (PaddingDirection direction, string padding) GetPadding(XElement element)
        {
            var padding = element.Attribute("padding")?.Value;
            if (padding is null)
            {
                padding = element.Attribute("lpadding")?.Value;
                if (padding is null)
                {
                    padding = element.Attribute("rpadding")?.Value ?? "";
                }
                else
                {
                    return (PaddingDirection.Left, padding);
                }
            }
            return (PaddingDirection.Right, padding);
        }

        /// <summary>
        /// XMLのノードを指定したStreamへの書き込み
        /// </summary>
        /// <param name="stream">バイナリの書き込みを行うStream(バッファリングが行われることを前提とする)</param>
        /// <param name="element">書き込み対象のXMLノード</param>
        public void Write(Stream stream, XElement element)
        {
            // 書き込み対象の文字列としての値
            var strValue = this.GetString(element);

            // 文字列としての評価結果をresult属性に追記
            element.SetAttributeValue("result", strValue);

            // 出力バイトオフセットの検証
            var offsetAttr = element.Attribute("offset");
            if (offsetAttr is not null)
            {
                stream.Seek(Int64.Parse(offsetAttr.Value), SeekOrigin.Begin);
            }

            // パディングに関するノード
            var (direction, padding) = GetPadding(element);
            // 出力エンコーディング
            var encoding = element.Attribute("encoding")?.Value;

            try
            {
                // 出力バイト数の検証
                int? bytes = this.GetBytes(element);
                if (bytes is not null)
                {
                    if (bytes.Value > 0)
                    {
                        if (strValue.Length == 0)
                        {
                            // パディングのみを書き込む場合
                            if (padding.Length > 0)
                            {
                                var paddingBuffer = StringToBytes(padding, encoding);
                                WritePadding(stream, bytes.Value, paddingBuffer);
                            }
                            else
                            {
                                WritePadding(stream, bytes.Value);
                            }
                        }
                        else
                        {
                            // バイト数を指定してバイト列へ変換
                            byte[] buffer = StringToBytes(strValue, encoding, bytes.Value);
                            if (bytes > buffer.Length)
                            {
                                // パディングの方向により本体とパディングの順序を入れ替える
                                int restLength = bytes.Value - buffer.Length;
                                if (padding.Length > 0)
                                {
                                    var paddingBuffer = StringToBytes(padding, encoding);
                                    if (direction == PaddingDirection.Right)
                                    {
                                        stream.Write(buffer);
                                    }
                                    WritePadding(stream, restLength, paddingBuffer);
                                }
                                else
                                {
                                    if (direction == PaddingDirection.Right)
                                    {
                                        stream.Write(buffer);
                                    }
                                    WritePadding(stream, restLength);
                                }
                                if (direction == PaddingDirection.Left)
                                {
                                    // left-paddingのときは後から本体を書き込む
                                    stream.Write(buffer);
                                }
                            }
                            else
                            {
                                // 切り詰めて出力
                                stream.Write(buffer, 0, bytes.Value);
                            }
                        }
                    }

                    // 出力バイト数の設定
                    element.SetAttributeValue("result-bytes", bytes.Value);
                }
                else if (strValue.Length > 0)
                {
                    // バイト幅の指定がない場合
                    byte[] buffer = StringToBytes(strValue, encoding);
                    stream.Write(buffer);

                    // 出力バイト数の設定
                    element.SetAttributeValue("result-bytes", buffer.Length);
                }
            }
            finally
            {
                if (offsetAttr is not null)
                {
                    // 最後の位置に戻す
                    stream.Seek(0, SeekOrigin.End);
                }
            }
        }
    }
}
