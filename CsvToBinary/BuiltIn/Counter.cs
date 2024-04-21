using System.Xml.Linq;
using System.Xml.XPath;

namespace BuiltIn
{
    /// <summary>
    /// Streamを介して連番を与えるためのクラス
    /// </summary>
    public class Counter : ICounter, IDisposable
    {
        /// <summary>
        /// カウンタに関するXMLが記載されたStream
        /// </summary>
        private readonly Stream stream;
        /// <summary>
        /// 入出力対象のXML
        /// </summary>
        private readonly XDocument doc;
        private readonly XElement root;
        /// <summary>
        /// カウンタ名とカウンタ値を紐づける連想配列
        /// </summary>
        private readonly Dictionary<string, long> counterMap = [];

        /// <summary>
        /// デフォルトのカウンタ
        /// </summary>
        private long defaultCounter = 0;

        /// <summary>
        /// 空のカウンタファイルの生成
        /// </summary>
        /// <param name="writer">書き込み先</param>
        public static void CreateEmpty(TextWriter writer)
        {
            (new XDocument(new XElement("counter"))).Save(writer);
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="stream">カウンタに関するXMLが記載されたStream</param>
        public Counter(Stream stream)
        {
            this.stream = stream;
            this.doc = XDocument.Load(stream);

            // Root要素の検証
            if (this.doc.Root is null) {
                // ここに来ることはない
                throw new InvalidOperationException("呼び出されることはありません");
            }
            else
            {
                if (this.doc.Root.Name != "counter")
                {
                    throw new ArgumentException("XMLのRoot要素はcounterである必要があります");
                }
                this.root = this.doc.Root;
            }

            // カウンタに関するXMLを構築する
            bool declaraDefaultCounter = false;
            foreach (XElement element in this.doc.XPathSelectElements("/counter/count"))
            {
                var nameAttr = element.Attribute("name");
                if (nameAttr is not null)
                {
                    if (this.counterMap.ContainsKey(nameAttr.Value))
                    {
                        throw new ArgumentException($"カウンタ名{nameAttr.Value}が多重定義されています");
                    }
                    else
                    {
                        this.counterMap.Add(nameAttr.Value, Int64.Parse(element.Value));
                    }
                }
                else
                {
                    if (declaraDefaultCounter)
                    {
                        throw new ArgumentException($"名前なしカウンタが多重定義されています");
                    }
                    this.defaultCounter = Int64.Parse(element.Value);
                    declaraDefaultCounter = true;
                }
            }
            // デフォルトカウンタが存在しなければ挿入しておく
            if (!declaraDefaultCounter)
            {
                this.root.Add(new XElement("count", this.defaultCounter));
            }
        }

        /// <summary>
        /// カウントアップを行い、現在のカウントを取得する
        /// </summary>
        /// <param name="name">カウンタの名称</param>
        /// <returns>現在のカウント</returns>
        public long Count(string? name = null)
        {
            if (name is not null)
            {
                if (this.counterMap.TryGetValue(name, out long value))
                {
                    // カウンタを更新
                    this.counterMap[name] = value + 1;
                }
                else
                {
                    // カウンタが存在しないときは0で初期化して挿入
                    value = 0;
                    this.counterMap.Add(name, value + 1);
                    // XMLにも挿入
                    this.root.Add(new XElement("count", new XAttribute("name", name), this.defaultCounter));
                }
                return value;
            }
            else
            {
                long value = this.defaultCounter;
                ++this.defaultCounter;
                return value;
            }
        }

        public void Dispose()
        {
            // XMLへ現在のカウンタに関する更新内容を書き出す
            foreach (XElement element in this.doc.XPathSelectElements("/counter/count"))
            {
                var nameAttr = element.Attribute("name");
                if (nameAttr is not null)
                {
                    element.Value = this.counterMap[nameAttr.Value].ToString();
                }
                else
                {
                    element.Value = this.defaultCounter.ToString();
                }
            }

            // Streamへ現在のカウンタに関する更新内容を書き出す
            this.stream.SetLength(0);
            this.doc.Save(this.stream);
            // streamは書き込み先でもあるため勝手にはクローズしないようにする
            //this.stream.Dispose();
        }
    }
}
