using System.Xml.Linq;
using CsvToBinary.BuiltIn;
using CsvToBinary.Data;
using CsvToBinary.Xml;

class Program
{
    /// <summary>
    /// （雑に）コマンドライン引数の解析を行う
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    /// <returns></returns>
    static public (Dictionary<string, string> externalDic, (IDataReader?, XmlDocumentWithPath) entry, List<(IDataReader?, XmlDocumentWithPath)> combined) ParseCommandLine(string[] args)
    {
        (IDataReader?, XmlDocumentWithPath)? entry = null;
        List<(IDataReader?, XmlDocumentWithPath)> combined = [];
        IDataReader? inputFile = null;
        Dictionary<string, string> externalDic = [];

        for (int i = 0; i < args.Length; ++i)
        {
            switch (args[i])
            {
                case "-xml":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("ファイル名の入力がありません");
                    }
                    ++i;
                    var path = Path.GetFullPath(args[i]);
                    if (entry is null)
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        entry = (inputFile, new XmlDocumentWithPath(XDocument.Load(stream), path));
                    }
                    else
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        combined.Add((inputFile, new XmlDocumentWithPath(XDocument.Load(stream), path)));
                    }
                    inputFile = null;
                    break;
                case "-csv":
                    if (inputFile is not null)
                    {
                        throw new ArgumentException($"[{args[i - 1]}]の次に来る引数に-csvを指定することはできません");
                    }
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("ファイル名の入力がありません");
                    }
                    ++i;
                    inputFile = new CsvReader(new StreamReader(args[i]));
                    break;
                //case "-bin":
                //    if (inputFile is not null)
                //    {
                //        throw new ArgumentException($"[{args[i - 1]}]の次に来る引数に-binを指定することはできません");
                //    }
                //    if (i + 1 >= args.Length)
                //    {
                //        throw new ArgumentException("ファイル名の入力がありません");
                //    }
                //    ++i;
                //    inputFile = new CsvReader(new StreamReader(args[i]));
                //    break;
                case "-g":
                    // 外部パラメータ
                    if (i + 2 >= args.Length)
                    {
                        throw new ArgumentException("外部パラメータの入力がありません");
                    }
                    externalDic[args[i + 1]] = args[i + 2];
                    i += 2;
                    break;
                default:
                    throw new ArgumentException($"不明なパラメータ[{args[i]}]が指定されています");
            }
        }

        if (entry is null)
        {
            throw new ArgumentException("XMLファイルなどの解析対象のファイルが入力されていません");
        }

        return (externalDic, entry.Value, combined);
    }

    /// <summary>
    /// 例外をテキスト形式で出力する
    /// </summary>
    /// <param name="writer">書き込み先</param>
    /// <param name="e">例外のインスタンス</param>
    static void DumpError(TextWriter writer, Exception e)
    {
        var current = e;
        int cnt = 0;
        while (current is not null)
        {
            writer.WriteLine($"[{cnt++}]");
            writer.WriteLine(current.Message);
            writer.WriteLine(current.StackTrace);
            current = current.InnerException;
        }
    }

    /// <summary>
    /// 異常終了時の共通処理
    /// </summary>
    /// <param name="msg">エラー時のe以外の固有メッセージ</param>
    /// <param name="e">例外のインスタンス</param>
    /// <returns>終了コード</returns>
    static int ErrorCommon(string? msg, Exception e)
    {
        if (msg is not null)
        {
            Console.Error.WriteLine(msg);
        }
        DumpError(Console.Error, e);
        Console.Error.WriteLine("任意のキーを押してください...");
        Console.ReadKey();
        return 1;
    }

    static public int Main(string[] args)
    {

        try
        {
            // コマンドライン引数の解析
            var (externalDic, entry, combined) = ParseCommandLine(args);

            // XPathの解決を行うオブジェクトの用意
            var xparhResolver = new XPathResolver();
            // 変換器の定義
            var transformControl = new TransformerControl(
                path =>
                {
                    try
                    {
                        using var stream = new StreamReader(path);
                        var doc = XDocument.Load(stream);
                        var type = doc.Root?.Attribute("type")?.Value;
                        return type switch
                        {
                            // 単純文字変換器
                            "chara-map" =>
                            new CharaTransformer(doc,
                                path =>
                                {
                                    // BOMを確認して適宜Unicode系で読み込む
                                    using var stream = new StreamReader(path, true);
                                    return stream.ReadToEnd();
                                }
                            ),
                            _ => throw new InputDataException($"型[{type}]に対応する変換器は存在しません", doc.Root)
                        };
                    }
                    catch (FileNotFoundException ex)
                    {
                        throw new InputDataException($"読み込み対象に指定されたXMLファイル[{path}]が存在しません", null, ex);
                    }
                }
            );
            // カウンタの定義
            var counterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "counter.xml");
            if (!File.Exists(counterPath))
            {
                // カウンタファイルが存在しないときは空の定義ファイルを作成
                using var writer = new StreamWriter(counterPath);
                Counter.CreateEmpty(writer);
            }
            using var counterFileStream = new FileStream(counterPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var counter = new Counter(counterFileStream);
            // XMLをバイナリへ変換するためのオブジェクトの用意
            var xmlToBinary = new XmlToBinary(transformControl, counter, xparhResolver, externalDic);
            // XMLを走査して変換するオブジェクトの用意
            var xmlTraverser = new XmlTraverser(
                xparhResolver,
                xmlToBinary,
                path =>
                {
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return XDocument.Load(stream);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InputDataException("読み込み対象に指定されたXMLファイルパスを構成する文字列に異常な文字が含まれています", null, ex);
                    }
                    catch (FileNotFoundException ex)
                    {
                        throw new InputDataException($"読み込み対象に指定されたXMLファイル[{path}]が存在しません", null, ex);
                    }
                },
                (type, name, xmlToBinary) =>
                {
                    try
                    {
                        return type switch
                        {
                            // バイナリファイルへの出力
                            "binary-file" => new CsvToBinary.Data.BinaryWriter(new FileStream(name, FileMode.Create, FileAccess.ReadWrite, FileShare.None), xmlToBinary),
                            _ => throw new InputDataException($"型[{type}]に対応する出力先は存在しません", null)
                        };
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InputDataException("書き込み先に指定されたファイルパスを構成する文字列に異常な文字が含まれています", null, ex);
                    }
                },
                externalDic
            );

            // データの変換
            Console.WriteLine("データ出力開始");
            int cnt = 0;
            // 読み込み可能な状態にしておく
            entry.Item1?.ReadChunk();
            foreach (var writer in xmlTraverser.Traversal(null, entry, combined)) using (writer)
            {
                Console.WriteLine($"{++cnt}件目のデータ出力完了");
            }
            Console.WriteLine("データ出力完了");
        }
        catch (InputDataException e)
        {
            return ErrorCommon("XMLのトラバース中に入力データに関する異常を検知しました", e);
        }
        catch (XmlTraverseException e)
        {
            return ErrorCommon("XMLのトラバース中に異常を検知しました", e);
        }
        catch (Exception e)
        {
            return ErrorCommon(null, e);
        }

        return 0;
    }
}
