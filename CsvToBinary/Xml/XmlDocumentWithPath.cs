using System.Xml.Linq;

namespace CsvToBinary.Xml
{
    /// <summary>
    /// パス情報付きのXMLドキュメント
    /// </summary>
    /// <param name="Document">XMLドキュメント</param>
    /// <param name="Path">XMLドキュメントへのパス</param>
    public record XmlDocumentWithPath(XDocument Document, string Path);
}
