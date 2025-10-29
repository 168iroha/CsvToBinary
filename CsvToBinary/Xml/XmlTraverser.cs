using CsvToBinary.Data;
using System.Xml.Linq;
using System.Xml.XPath;

namespace CsvToBinary.Xml
{

    /// <summary>
    /// XMLを走査してStreamへデータを出力するクラス
    /// </summary>
    /// <param name="xPathResolver">XPathの解決のためのインスタンス</param>
    /// <param name="xmlToBinary">XMLをStreamへ書き出す処理が記載されたインスタンス</param>
    /// <param name="xmlFunc">外部XMLを読み込むための関数</param>
    /// <param name="writerFunc">外部XMLを読み込むための関数</param>
    /// <param name="externalDic">変換時に外部から与えるパラメータ</param>
    public class XmlTraverser(IXPathResolver xPathResolver, IXmlToBinary xmlToBinary, Func<string, XDocument> xmlFunc, Func<string, string, IXmlToBinary, IDataWriter> writerFunc, Dictionary<string, string> externalDic)
    {
        /// <summary>
        /// 結合するために用いるXMLのためのクラス
        /// </summary>
        /// <param name="combined">逐次結合されるXML</param>
        private class CombinedXml(List<(IDataReader?, XmlDocumentWithPath)> combined)
        {
            /// <summary>
            /// 逐次結合されるXML
            /// </summary>
            private readonly List<(IDataReader?, XmlDocumentWithPath)> combined = combined;
            /// <summary>
            /// 現在参照しているcombined
            /// </summary>
            public int? CurrentComblinedIndex { get; private set; }

            /// <summary>
            /// 現在参照している結合されるXMLの取得
            /// </summary>
            /// <returns>現在参照している結合されるXML</returns>
            public (IDataReader?, XmlDocumentWithPath)? Current()
            {
                if (this.CurrentComblinedIndex is not null && this.CurrentComblinedIndex < this.combined.Count)
                {
                    return this.combined[(int)this.CurrentComblinedIndex];
                }
                return null;
            }

            // 結合されるXMLの数の取得
            public int Count => this.combined.Count;

            /// <summary>
            /// 次の結合されるXMLの参照へ移動
            /// </summary>
            /// <returns>移動に成功した場合にtrue</returns>
            public bool Next()
            {
                if (this.CurrentComblinedIndex is null && this.combined.Count > 0)
                {
                    // まだXMLを一度も参照したことがない場合
                    this.CurrentComblinedIndex = 0;
                }
                else if (this.CurrentComblinedIndex is not null && this.CurrentComblinedIndex < this.combined.Count)
                {
                    ++this.CurrentComblinedIndex;
                }
                if (this.CurrentComblinedIndex is null)
                {
                    // 結合されるXML自体が存在しない場合
                    return false;
                }
                return this.CurrentComblinedIndex < this.combined.Count;
            }
        }

        /// <summary>
        /// repeatの実装のための抽象クラス
        /// </summary>
        private abstract class ARepeat
        {
            /// <summary>
            /// ループカウンタ
            /// </summary>
            public int Count { get; private set; }

            /// <summary>
            /// ループ展開を行うか
            /// </summary>
            private readonly bool unrolling = false;

            /// <summary>
            /// ループの項目についてフェッチを行うか
            /// </summary>
            protected readonly bool fetch;

            /// <summary>
            /// ループの最大実行回数
            /// </summary>
            public int? MaxCount { get; private set; }

            /// <summary>
            /// elementの位置を示すキー
            /// </summary>
            private readonly string key;

            /// <summary>
            /// elementを示すキー
            /// </summary>
            private readonly string elementKey;

            /// <summary>
            /// XMLの深さ優先探索のためのスタック(兄弟要素を走査する)
            /// </summary>
            private readonly Stack<(string, XElement)> scanStack;

            /// <summary>
            /// ループの起点のrepeat要素
            /// </summary>
            protected XElement element;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="inst">親クラス</param>
            /// <param name="scanStack">XMLの深さ優先探索のためのスタック(兄弟要素を走査する)</param>
            /// <param name="element">ループの起点のrepeat要素</param>
            /// <param name="key">elementの位置を示すキー</param>
            /// <param name="fetchDefault">ループの項目についてフェッチを行うかのデフォルト値</param>
            public ARepeat(XmlTraverser inst, Stack<(string, XElement)> scanStack, XElement element, string key, bool fetchDefault)
            {
                this.scanStack = scanStack;
                this.element = element;
                // ループ展開を行うかの設定
                var unrolling = element.Attribute("unrolling")?.Value;
                if (unrolling is not null)
                {
                    this.unrolling = Boolean.Parse(unrolling);
                }
                // ループの項目についてフェッチを行うかの設定
                this.fetch = fetchDefault;
                var fetch = element.Attribute("fetch")?.Value;
                if (fetch is not null)
                {
                    this.fetch = Boolean.Parse(fetch);
                }
                this.key = key;
                this.elementKey = GetElementKey(key, element);

                // ループ最大回数の設定
                var maxCount = element.Attribute("max")?.Value;
                if (maxCount is null)
                {
                    // maxの指定がないときはxmaxのXPathを評価してループ最大回数を得る
                    var xmaxCount = element.Attribute("xmax")?.Value;
                    if (xmaxCount is not null)
                    {
                        maxCount = inst.xPathResolver.XPathEvaluate(element, xmaxCount);
                    }
                }
                if (maxCount is not null)
                {
                    this.MaxCount = Int32.Parse(maxCount);
                    if (this.MaxCount < 0)
                    {
                        throw new InputDataException("repeat/@maxに負数を設定することはできません", element);
                    }
                }
                this.key = key;
                this.elementKey = GetElementKey(key, element);

                // ループ展開された対象の一番最後以外の要素の削除
                string id = element.Attribute("repeat-id")!.Value;
                List<XElement> removeList = [element];
                foreach (var sibling in element.ElementsAfterSelf())
                {
                    if (sibling.Name.LocalName == "repeat" && sibling.Attribute("repeat-id")!.Value == id)
                    {
                        removeList.Add(sibling);
                    }
                    else
                    {
                        break;
                    }
                }
                removeList.RemoveAt(removeList.Count - 1);
                foreach (var node in removeList)
                {
                    node.Remove();
                }
                element.SetAttributeValue("seq", 0);
            }

            /// <summary>
            /// 次のループの開始を試みる
            /// </summary>
            /// <returns>ループの開始に成功したときにtrue</returns>
            public abstract bool Next();

            /// <summary>
            /// ループの開始の宣言
            /// </summary>
            /// <returns>ループの開始に成功したときにtrue</returns>
            protected virtual bool Start()
            {
                ++this.Count;
                if (!this.element.HasElements || (this.MaxCount is not null && this.Count > this.MaxCount))
                {
                    // 解析する対象が存在しない場合あるいはループの最大件数に到達した場合は終了
                    this.Finish();
                    return false;
                }
                if (this.Count > 1)
                {
                    if (this.unrolling)
                    {
                        // ループ展開される際は前回のループの内容を複製して設定する
                        var target = new XElement(this.element);
                        this.element.AddAfterSelf(target);
                        this.element = target;
                    }
                    this.element.SetAttributeValue("seq", this.Count - 1);
                }

                // 兄弟の解析を中断して一番最初の子を次から解析するようにする
                this.scanStack.Push((this.elementKey, this.element.Elements().First()));
                return true;
            }

            /// <summary>
            /// ループの終了の宣言
            /// </summary>
            protected virtual void Finish()
            {
                // 兄弟要素の解析のセットアップ
                var siblings = this.element.ElementsAfterSelf();
                if (siblings.Any())
                {
                    this.scanStack.Push((this.key, siblings.First()));
                }
                // ループ終了時に位置の取得は可能のためNOPの挿入は不要
            }
        }

        /// <summary>
        /// 結合されるXMLのためのrepeatの実装
        /// </summary>
        /// <param name="inst">親クラス</param>
        /// <param name="scanStack">XMLの深さ優先探索のためのスタック(兄弟要素を走査する)</param>
        /// <param name="element">ループの起点のrepeat要素</param>
        /// <param name="key">elementの位置を示すキー</param>
        /// <param name="combinedXml">結合するために用いるXML</param>
        private class RepeatCombinedXml(XmlTraverser inst, Stack<(string, XElement)> scanStack, XElement element, string key, XmlTraverser.CombinedXml combinedXml) : ARepeat(inst, scanStack, element, key, true)
        {
            /// <summary>
            /// 結合するために用いるXML
            /// </summary>
            private readonly CombinedXml combinedXml = combinedXml;

            /// <summary>
            /// ループに関する前回の参照点
            /// </summary>
            private int? prevReference = combinedXml.CurrentComblinedIndex;

            /// <summary>
            /// 次のループの開始を試みる
            /// </summary>
            /// <returns>ループの開始に成功したときにtrue</returns>
            public override bool Next()
            {
                if (this.fetch)
                {
                    if (!this.combinedXml.Next())
                    {
                        // 次の結合されたXMLが存在しない場合は終了
                        this.Finish();
                        return false;
                    }
                    // 読み込み可能な状態にしておく
                    this.combinedXml.Current()!.Value.Item1?.ReadChunk();
                }
                else if (this.combinedXml.Count == this.prevReference && this.combinedXml.Count == this.combinedXml.CurrentComblinedIndex)
                {
                    // 前回が最後の結合されたXMLに関するループの場合は終了
                    this.Finish();
                    return false;
                }
                else
                {
                    // 初回呼び出し以降で前回と同じXMLを参照している場合は無限ループとして処理
                    if (this.Count != 0 && this.prevReference == this.combinedXml.CurrentComblinedIndex)
                    {
                        throw new InfiniteLoopException("repeat/[@type='combined-xml']について無限ループとなる可能性のある個所が存在します", this.element);
                    }
                    this.prevReference = this.combinedXml.CurrentComblinedIndex;
                }

                // ループは継続されるためセットアップをする
                return this.Start();
            }
        }

        /// <summary>
        /// 結合対象の1つのファイルのレコードの読み込みのためのrepeatの実装
        /// </summary>
        /// <param name="inst">親クラス</param>
        /// <param name="scanStack">XMLの深さ優先探索のためのスタック(兄弟要素を走査する)</param>
        /// <param name="element">ループの起点のrepeat要素</param>
        /// <param name="key">elementの位置を示すキー</param>
        /// <param name="combinedXml">結合するために用いるXML</param>
        private class RepeatCombinedRecord(XmlTraverser inst, Stack<(string, XElement)> scanStack, XElement element, string key, XmlTraverser.CombinedXml combinedXml) : ARepeat(inst, scanStack, element, key, true)
        {
            /// <summary>
            /// 結合するために用いるXML
            /// </summary>
            private readonly CombinedXml combinedXml = combinedXml;

            /// <summary>
            /// ループに関する前回の参照点
            /// </summary>
            private int? prevReference = combinedXml.CurrentComblinedIndex;

            /// <summary>
            /// 次のループの開始を試みる
            /// </summary>
            /// <returns>ループの開始に成功したときにtrue</returns>
            public override bool Next()
            {
                var current = this.combinedXml.Current() ?? throw new XmlFormatException("結合されるXMLを走査していない状態でrepeat/[@type='combined-record']を解析しようとしています", element);
                // 初回はレコードを読み込まない
                if (this.Count != 0 && this.fetch)
                {
                    if ((this.MaxCount is not null && this.Count >= this.MaxCount) || !(current.Item1?.ReadChunk() ?? false))
                    {
                        // 読み込み対象のレコードが存在しない場合は終了
                        this.Finish();
                        return false;
                    }
                }
                else if (!(current.Item1?.Valid() ?? false))
                {
                    // 前回が最後の結合されたXMLに関するループの場合は終了
                    this.Finish();
                    return false;
                }
                else
                {
                    var reader = current.Item1!;
                    // 初回呼び出し以降で前回と同じXMLを参照している場合は無限ループとして処理
                    if (this.Count != 0 && this.prevReference == reader.GetChunkId())
                    {
                        throw new InfiniteLoopException("repeat/[@type='combined-record']について無限ループとなる可能性のある個所が存在します", this.element);
                    }
                    this.prevReference = reader.GetChunkId();
                }

                // ループは継続されるためセットアップをする
                return this.Start();
            }
        }

        /// <summary>
        /// 指定された回数ループするためのrepeatの実装
        /// </summary>
        private class RepeatCount : ARepeat
        {
            /// <summary>
            /// データの読み込み元
            /// </summary>
            private readonly IDataReader? reader;

            /// <summary>
            /// レコードをスタックしたか
            /// </summary>
            private bool stacked = false;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="inst">親クラス</param>
            /// <param name="scanStack">XMLの深さ優先探索のためのスタック(兄弟要素を走査する)</param>
            /// <param name="element">ループの起点のrepeat要素</param>
            /// <param name="key">elementの位置を示すキー</param>
            /// <param name="reader">データの読み込み元</param>
            public RepeatCount(XmlTraverser inst, Stack<(string, XElement)> scanStack, XElement element, string key, IDataReader? reader) : base(inst, scanStack, element, key, false)
            {
                this.reader = reader;
                if (!this.fetch && this.MaxCount is null)
                {
                    throw new InfiniteLoopException("repeat/@maxあるいはrepeat/@xmaxに整数値を設定する必要があります", this.element);
                }
            }

            /// <summary>
            /// 次のループの開始を試みる
            /// </summary>
            /// <returns>ループの開始に成功したときにtrue</returns>
            public override bool Next()
            {
                // 初回はレコードを読み込まない
                if (this.Count != 0 && this.fetch)
                {
                    if (this.Count == 1)
                    {
                        // 初回のReadChunk()前の場合は読み込み済みのレコードを退避しておく
                        this.reader?.Push();
                        this.stacked = true;
                    }
                    if ((this.MaxCount is not null && this.Count >= this.MaxCount) || !(this.reader?.ReadChunk() ?? true))
                    {
                        // 読み込み対象のデータが存在しない場合は終了
                        this.Finish();
                        return false;
                    }
                }
                // ループは継続されるためセットアップをする
                return this.Start();
            }

            /// <summary>
            /// ループの終了の宣言
            /// </summary>
            protected override void Finish()
            {
                if (this.stacked)
                {
                    // Next()で退避したレコードを元に戻す
                    this.reader?.Pop();
                }
                base.Finish();
            }
        }

        /// <summary>
        /// XPathの解決のためのインスタンス
        /// </summary>
        private readonly IXPathResolver xPathResolver = xPathResolver;

        /// <summary>
        /// XMLをStreamへ書き出す処理が記載されたインスタンス
        /// </summary>
        private readonly IXmlToBinary xmlToBinary = xmlToBinary;

        /// <summary>
        /// 外部XMLに関するマップ
        /// </summary>
        private readonly Dictionary<string, XmlDocumentWithPath> xmlDict = [];

        /// <summary>
        /// 外部XMLを読み込むための関数
        /// </summary>
        private readonly Func<string, XDocument> xmlFunc = xmlFunc;

        /// <summary>
        /// 書き込み先を得るための関数
        /// </summary>
        private readonly Func<string, string, IXmlToBinary, IDataWriter> writerFunc = writerFunc;

        /// <summary>
        /// 変換時に外部から与えるパラメータ
        /// </summary>
        private readonly Dictionary<string, string> externalDic = externalDic;

        /// <summary>
        /// 何もしないことを示すノードの取得
        /// </summary>
        /// <returns></returns>
        private static XElement GetNop()
        {
            return new XElement("nop");
        }

        /// <summary>
        /// elementを示すキーの取得
        /// </summary>
        /// <param name="key">elementの位置を示すキー</param>
        /// <param name="element">キーを計算する対象</param>
        /// <returns></returns>
        private static string GetElementKey(string key, XElement element)
        {
            var name = element.Attribute("name")?.Value ?? "";
            return key.Length == 0 ? name : name.Length == 0 ? key : $"{key}/{name}";
        }

        /// <summary>
        /// XMLの読み込み
        /// </summary>
        /// <param name="target">読み込み対象</param>
        /// <param name="relative">読み込み元となるパス</param>
        /// <returns>読み込み結果</returns>
        private XmlDocumentWithPath ImportXml(string target, string relative)
        {
            var parentPath = Path.GetDirectoryName(relative);
            // 相対パスなら実行ファイルの位置を基準としてパスを構築して読み込む
            var path = Path.GetFullPath(
                Path.IsPathRooted(target) ? target :
                string.IsNullOrEmpty(parentPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, target) :
                Path.Combine(parentPath, target)
                );
            if (!this.xmlDict.TryGetValue(path, out XmlDocumentWithPath? doc))
            {
                doc = new XmlDocumentWithPath(this.xmlFunc(path), path);
                this.xmlDict[path] = doc;
            }
            return doc;
        }

        /// <summary>
        /// トラバーサルで走査に用いるXMLの読み込み
        /// </summary>
        /// <param name="importNode">読み込み対象が記載されたノード</param>
        /// <param name="relative">読み込み元となるパス</param>
        /// <returns>読み込み結果</returns>
        private XmlDocumentWithPath ImportXml(XElement importNode, string relative)
        {
            // 読み込むXMLのパスの取得
            var target = importNode.Attribute("target")?.Value;
            if (target is null)
            {
                // targetの指定がないときはxtargetのXPathを評価してXMLのパスを得る
                var xtarget = importNode.Attribute("xtarget")?.Value;
                if (xtarget is not null)
                {
                    target = this.xPathResolver.XPathEvaluate(importNode, xtarget);
                }
            }
            if (target is null)
            {
                throw new XmlFormatException("import[@type='xml']を指定した場合、targetあるいはxtargetの指定は必須です", importNode);
            }
            // XMLを読み込んでディープコピーを返す
            var doc = this.ImportXml(target, relative);
            return new XmlDocumentWithPath(new XDocument(doc.Document), doc.Path);
        }

        /// <summary>
        /// XMLを読み込み結果を編集する
        /// </summary>
        /// <param name="element">編集情報が記載された要素</param>
        /// <param name="targetDoc">読み込む対象のXML</param>
        /// <param name="relative">読み込み元となるパス</param>
        /// <exception cref="ArgumentException"></exception>
        private void EditXml(XElement element, XmlDocumentWithPath targetDoc, string relative)
        {
            // targetで読み込んだXMLの変換のために用いるXMLファイル名
            var transform = element.Attribute("transform")?.Value;

            XmlDocumentWithPath? transformDoc = null;
            if (transform is not null)
            {
                // 変換のために用いるXMLの取得
                transformDoc = this.ImportXml(transform, relative);
            }
            // 変換規則に従って変換をする
            foreach (var map in this.xPathResolver.XPathSelectElements(element, "./map"))
            {
                var value = map.Value;
                var typeAttr = map.Attribute("type");
                var fromAttr = map.Attribute("from") ?? throw new XmlFormatException("import[@type='xml']/mapにはfrom属性が必須です", element);
                var replace = typeAttr?.Value switch
                {
                    // そのまま
                    "text" => value,
                    // 外部から与えられるマップで変換
                    "external" => externalDic.TryGetValue(value, out string? v) ? v : "",
                    // XPathを用いて変換
                    _ => this.xPathResolver.XPathEvaluate(transformDoc?.Document ?? throw new XmlFormatException("import[@type='xml']/map[@from]のときimportにはtransform属性が必須です", element), value)
                };
                // 置換の実行
                foreach (XPathNavigator nav in this.xPathResolver.XPathSelectNodes(targetDoc.Document, fromAttr.Value))
                {
                    switch (nav.NodeType)
                    {
                        case XPathNodeType.Element:
                            ((XElement)nav.UnderlyingObject!).Value = replace;
                            break;
                        case XPathNodeType.Text:
                            ((XText)nav.UnderlyingObject!).Value = replace;
                            break;
                        case XPathNodeType.Attribute:
                            ((XAttribute)nav.UnderlyingObject!).Value = replace;
                            break;
                        default:
                            throw new XmlFormatException($"[{nav.NodeType}]というXPathNodeTypeはサポートしていません", element);
                    }
                }
            }
        }

        /// <summary>
        /// import[@type='xml']の解決を行う
        /// </summary>
        /// <param name="root">解決対象の要素</param>
        /// <exception cref="ArgumentException"></exception>
        private void ResolveImportXml(XmlDocumentWithPath root)
        {
            Stack<XmlDocumentWithPath> dfsStack = [];
            Stack<(XElement ImportNode, XmlDocumentWithPath TargetDoc, string Relative)> backtrackStack = [];
            dfsStack.Push(root);

            // 深さ優先探索でループ
            while (dfsStack.Count > 0)
            {
                var (node, relative) = dfsStack.Pop();

                foreach (var importNode in this.xPathResolver.XPathSelectElements(node, "//import[@type='xml']"))
                {
                    var targetDoc = this.ImportXml(importNode, relative);
                    backtrackStack.Push((importNode, targetDoc, relative));
                    dfsStack.Push(targetDoc);
                }
            }

            // バックトラックして読み込んだXMLを子から順に1つのXMLにする
            while (backtrackStack.Count > 0)
            {
                var (importNode, targetDoc, relative) = backtrackStack.Pop();
                var name = importNode.Attribute("name")?.Value ?? "";

                // importNodeをtargetDocのXMLに置換する(import[@name='xxx']をitems[@name='xxx']に丸ごと入れ替える)
                this.EditXml(importNode, targetDoc, relative);
                importNode.ReplaceAll(new XAttribute("name", name), targetDoc.Document.Root!.Elements());
                importNode.Name = "items";
            }
        }

        /// <summary>
        /// writerの書き込みが生じるようなノードの解析
        /// </summary>
        /// <param name="writer">書き込み先</param>
        /// <param name="reader">データの読み込み元</param>
        /// <param name="key">elementの位置を示すキー</param>
        /// <param name="element">解析対象の要素</param>
        /// <param name="combinedXml">現在解析対象となっている結合されるXml</param>
        private static void TraversalItemNode(IDataWriter? writer, IDataReader? reader, string key, XElement element, CombinedXml combinedXml)
        {
            var elementKey = GetElementKey(key, element);
            var type = element.Attribute("type")?.Value;

            // readerなどからデータの取り出し
            string? value = null;
            if (type == "combined")
            {
                var currentCombined = combinedXml.Current();
                if (currentCombined is not null)
                {
                    // 結合されるXMLのデータ取得の場合
                    value = currentCombined.Value.Item1?.GetData(elementKey, element) ?? "";
                }
            }
            value ??= reader?.GetData(elementKey, element) ?? "";

            // valueノードの子にvalueを設定
            var valueNode = element.Element("value");
            if (valueNode is null)
            {
                valueNode = new XElement("value", value);
                element.AddFirst(valueNode);
            }
            else// if (valueNode.Value.Length == 0)
            {
                valueNode.Value = value;
            }
            // データを書き出す
            writer?.SetData(elementKey, element);
        }

        /// <summary>
        /// itemsノードの解析
        /// </summary>
        /// <param name="key">elementの位置を示すキー</param>
        /// <param name="element">解析対象の要素</param>
        /// <param name="scanStack">探索のための</param>
        private static void TraversalItemsNode(string key, XElement element, Stack<(string, XElement)> scanStack)
        {
            var elementKey = GetElementKey(key, element);

            // データの集まりの書き出し
            if (element.HasElements)
            {
                // 兄弟の解析を中断して一番最初の子を次から解析するようにする
                var siblings = element.ElementsAfterSelf();
                if (siblings.Any())
                {
                    scanStack.Push((key, siblings.First()));
                }
                else if (element != element.Document?.Root)
                {
                    // 復帰位置を記憶するためにNOPを挿入
                    var nop = GetNop();
                    element.AddAfterSelf(nop);
                    scanStack.Push((key, nop));
                }
                scanStack.Push((elementKey, element.Elements().First()));
            }
        }

        /// <summary>
        /// writeノードの解析
        /// </summary>
        /// <param name="writer">書き込み先</param>
        /// <param name="reader">データの読み込み元</param>
        /// <param name="element">解析対象の要素</param>
        /// <param name="combinedXml">現在解析対象となっている結合されるXml</param>
        private void TraversalWriteNode(out IDataWriter writer, IDataReader? reader, XElement element, CombinedXml combinedXml)
        {
            var type = element.Attribute("type")?.Value ?? "";
            var key = element.Attribute("key")?.Value ?? "";

            // stringWriterへelementの内容を出力する
            using var stringWriter = new Data.StringWriter(new MemoryStream(), this.xmlToBinary);
            Traversal(stringWriter, (reader, element), combinedXml, key);

            // 書き込み先IDataWriterの選択
            writer = this.writerFunc(type, stringWriter.GetString(), this.xmlToBinary);
        }

        /// <summary>
        /// importノードの解析
        /// </summary>
        /// <param name="reader">データの読み込み元</param>
        /// <param name="writer">書き込み先</param>
        /// <param name="element">解析対象の要素</param>
        /// <param name="relative">読み込み元となるパス</param>
        /// <param name="combinedXml">現在解析対象となっている結合されるXml</param>
        /// <param name="scanStack">探索のための</param>
        private IEnumerable<IDataWriter> TraversalImportNode(IDataReader? reader, IDataWriter? writer, XElement element, string relative, CombinedXml combinedXml, Stack<(string, XElement)> scanStack)
        {
            var type = element.Attribute("type")?.Value;

            // 入力元の切り替え
            switch (type)
            {
                case "combined":
                    // 結合されたXMLの解析へ制御を移す
                    var current = combinedXml.Current() ?? throw new XmlFormatException("結合されるXMLを走査していない状態でimport/[@type='combined']を解析しようとしています", element);
                    this.EditXml(element, current.Item2, relative);
                    return this.Traversal(writer, current, []);
                case "dynamic":
                    // 制御を移さずに読み込む
                    var targetDoc = this.ImportXml(element, relative);
                    var root = targetDoc.Document.Root;
                    if (root is not null)
                    {
                        // XMLのimportの解決は実施せず、毎回importを評価する
                        // importしたXML内のimportの解決はする
                        this.EditXml(element, targetDoc, relative);
                        return this.Traversal(writer, (reader, targetDoc), []);
                    }
                    break;
                case "none":
                    // 切り替えを実施しない
                    break;
                default:
                    throw new XmlFormatException($"import/@typeに対する値{type}は不明です", element);
            }
            return [];
        }

        /// <summary>
        /// repeatノードの解析
        /// </summary>
        /// <param name="reader">データの読み込み元</param>
        /// <param name="key">elementの位置を示すキー</param>
        /// <param name="element">解析対象の要素</param>
        /// <param name="combinedXml">現在解析対象となっている結合されるXml</param>
        /// <param name="scanStack">探索のためのスタック</param>
        /// <param name="repeatStack">ループのためのスタック</param>
        private void TraversalRepeatNode(
            IDataReader? reader,
            string key,
            XElement element,
            CombinedXml combinedXml,
            Stack<(string, XElement)> scanStack,
            Stack<ARepeat> repeatStack
        )
        {
            var type = element.Attribute("type")?.Value;

            // ループの終了条件の種類についての分岐
            ARepeat repeatObj = type switch
            {
                // 結合されるXMLの全ての処理が完了するまでループする
                "combined-xml" => new RepeatCombinedXml(this, scanStack, element, key, combinedXml),
                // 結合対象の1つのファイルのレコードの読み込みが完了するまでループする
                // maxと組み合わせることで100件単位でのフェッチなどが可能
                "combined-record" => new RepeatCombinedRecord(this, scanStack, element, key, combinedXml),
                // 指定された回数ループする
                null or "" => new RepeatCount(this, scanStack, element, key, reader),
                _ => throw new XmlFormatException($"repeat/@typeに対する値{type}は不明です", element),
            };

            // 初期状態の構築
            repeatObj.Next();
            repeatStack.Push(repeatObj);
        }

        /// <summary>
        /// XMLのトラバーサルを行う
        /// </summary>
        /// <param name="writer">書き込み先</param>
        /// <param name="entry">トラバーサルのエントリポイントとなるXML</param>
        /// <param name="combined">entryに対して逐次結合されるXML</param>
        /// <param name="globalKey">entryの位置を示すキー</param>
        public IEnumerable<IDataWriter> Traversal(IDataWriter? writer, (IDataReader?, XmlDocumentWithPath) entry, List<(IDataReader?, XmlDocumentWithPath)> combined, string globalKey = "")
        {
            var root = entry.Item2.Document.Root;
            // 書き込み終了の通知が必要でないかを示すフラグ
            bool noEndtoWriting = writer is not null;

            if (root is not null && root.HasElements)
            {
                var reader = entry.Item1;
                // 外部の参照が行われるXMLを1つのファイルにまとめる
                this.ResolveImportXml(entry.Item2);
                // repeatにidを付与する
                int repeatCnt = 0;
                foreach (var repeatNode in this.xPathResolver.XPathSelectElements(root, "//repeat"))
                {
                    repeatNode.SetAttributeValue("repeat-id", repeatCnt++);
                }

                // XMLの深さ優先探索のためのスタック(兄弟要素を走査する)
                Stack<(string, XElement)> scanStack = [];
                scanStack.Push((globalKey, root.Elements().First()));
                // repeatのためのスタック
                Stack<ARepeat> repeatStack = [];
                // 現在解析対象となっている結合されるXml
                var combinedXml = new CombinedXml(combined);

                // 書き込み可能な状態にしておく
                writer?.Push();
                // writerがnullの場合などwriterの実装依存しない実際の深さ
                int depth = 1;

                while (scanStack.Count > 0)
                {
                    var (key, element) = scanStack.Pop();
                    var count = scanStack.Count;
                    foreach (XElement sibling in element.ElementsAfterSelf().Prepend(element))
                    {
                        // siblingに対してタグによる分岐の実施
                        switch (sibling.Name.LocalName)
                        {
                            case "item":
                                TraversalItemNode(writer, reader, key, sibling, combinedXml);
                                break;
                            case "items":
                                TraversalItemsNode(key, sibling, scanStack);
                                break;
                            case "writer":
                                if (writer is not null)
                                {
                                    // 書き込み終了の通知
                                    while (writer.Depth() != 0)
                                    {
                                        writer.WriteChunk();
                                        writer.Pop();
                                    }
                                    yield return writer;
                                }
                                this.TraversalWriteNode(out writer, reader, sibling, combinedXml);
                                // 書き込み開始のために深さの数だけスタックにpushする
                                for (int i = 0; i < depth; ++i)
                                {
                                    writer.Push();
                                }
                                break;
                            case "import":
                                foreach (var ret in this.TraversalImportNode(reader, writer, sibling, entry.Item2.Path, combinedXml, scanStack))
                                {
                                    yield return ret;
                                }
                                break;
                            case "repeat":
                                this.TraversalRepeatNode(reader, key, sibling, combinedXml, scanStack, repeatStack);
                                break;
                            case "nop":
                                break;
                            default:
                                throw new XmlFormatException($"{sibling.Name.LocalName}というタグ名は不明です", element);
                        }
                        if (count != scanStack.Count)
                        {
                            break;
                        }
                    }

                    // 子要素の走査が中断せずに完了したとき
                    if (count == scanStack.Count)
                    {
                        var target = element;
                        // ループの解決を試みる
                        while (true)
                        {
                            var parent = target.Parent!;
                            if (parent.Name.LocalName == "repeat")
                            {
                                if (parent.Attribute("seq")?.Value != "0")
                                {
                                    // 2回目以降のループの終端の場合は書き出す
                                    writer?.WriteChunk();
                                }
                                else
                                {
                                    // 初回のループ実施後はスタックする
                                    writer?.Push();
                                    ++depth;
                                }
                                if (!repeatStack.Peek().Next())
                                {
                                    // ループ条件を満たさないとき
                                    repeatStack.Pop();
                                    writer?.Pop();
                                    --depth;
                                    
                                    if (repeatStack.Count != 0 && !parent.ElementsAfterSelf().Any() && parent.Parent!.Name.LocalName == "repeat")
                                    {
                                        // 次の解析する対象(parentの次の要素)もループの終端のときはparentの親に関するループの解決も行う
                                        target = parent;
                                        continue;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                if (writer is not null)
                {
                    // 書き込み終了の通知
                    writer.WriteChunk();
                    writer.Pop();
                    // --depth;
                    if (!noEndtoWriting)
                    {
                        yield return writer;
                    }
                }
            }
        }

        /// <summary>
        /// 文字列要素取得のためのXMLのトラバーサルを行う<br />
        /// writerへの書き込み以外はできないものとする
        /// </summary>
        /// <param name="writer">書き込み先</param>
        /// <param name="entry">トラバーサルのエントリポイントとなるXML(readerによるデータ読み込みは行われずカレントの読み込みのみが有効)</param>
        /// <param name="globalKey">entryの位置を示すキー</param>
        private static void Traversal(Data.StringWriter writer, (IDataReader?, XElement) entry, CombinedXml combinedXml, string globalKey)
        {
            var reader = entry.Item1;

            // XMLの深さ優先探索のためのスタック(兄弟要素を走査する)
            Stack<(string, XElement)> scanStack = [];
            scanStack.Push((globalKey, entry.Item2.Elements().First()));

            while (scanStack.Count > 0)
            {
                var (key, element) = scanStack.Pop();
                var count = scanStack.Count;
                foreach (XElement sibling in element.ElementsAfterSelf().Prepend(element))
                {
                    // siblingに対してタグによる分岐の実施
                    switch (sibling.Name.LocalName)
                    {
                        case "item":
                            TraversalItemNode(writer, reader, key, sibling, combinedXml);
                            break;
                        case "items":
                            TraversalItemsNode(key, sibling, scanStack);
                            break;
                        // 必要になったらサポートする
                        //case "repeat":
                        //    this.TraversalRepeatNode(reader, key, sibling, combinedXml, scanStack, repeatStack);
                        //    break;
                        case "nop":
                            break;
                        default:
                            throw new XmlFormatException($"{sibling.Name.LocalName}というタグ名は不明です", element);
                    }
                    if (count != scanStack.Count)
                    {
                        break;
                    }
                }
            }
            writer.WriteChunk();
        }
    }
}
