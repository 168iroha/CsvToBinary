using System.Text;
using System.Xml.Linq;
using tests.Xml;
using BinaryWriter = CsvToBinary.Data.BinaryWriter;

namespace tests.Data
{
    [TestClass]
    public class BinaryWriterTests
    {
        /// <summary>
        /// WriteChunkの実行
        /// </summary>
        const int COM_WRITE_CHUNK = -1;
        /// <summary>
        /// Pushの実行
        /// </summary>
        const int COM_PUSH = -2;
        /// <summary>
        /// Popの実行
        /// </summary>
        const int COM_POP = -3;

        /// <summary>
        /// データの書き込みのシミュレートを行う
        /// </summary>
        /// <param name="elementList">書き込み対象のXElementのリスト</param>
        /// <param name="commandList">実行するコマンドのリスト</param>
        /// <returns>書き込み結果のバイナリ列</returns>
        public static byte[] WriteSimulate(List<XElement> elementList, int[] commandList)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream, new StubXmlToBinary());
            foreach (int command in commandList)
            {
                switch (command)
                {
                    case COM_WRITE_CHUNK:
                        writer.WriteChunk();
                        break;
                    case COM_PUSH:
                        writer.Push();
                        break;
                    case COM_POP:
                        writer.Pop();
                        break;
                    default:
                        // 明示的なコマンド以外はelementListのインデックス
                        writer.SetData("", elementList[command]);
                        break;
                }
            }
            return stream.ReadAll();
        }

        [TestMethod]
        public void 単要素の書き込みの確認()
        {
            var item = "あ";
            // 検査対象のXELemenet
            List<XElement> elementList = [
                 new XElement("item", new XElement("value", item))
            ];
            // 書き込みの検査
            CollectionAssert.AreEqual(
                WriteSimulate(elementList, [COM_PUSH, 0, COM_POP]),
                Encoding.UTF8.GetBytes(item)
            );
        }

        [TestMethod]
        public void 遅延書き込みの確認()
        {
            // 検査対象のXELemenet
            List<XElement> elementList = [
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "あ")),
                 new XElement("item", new XElement("value", "い"))
            ];
            // 書き込みの検査
            CollectionAssert.AreEqual(
                WriteSimulate([.. elementList.Select(x => new XElement(x))], [
                    COM_PUSH, 0, 1, COM_WRITE_CHUNK, COM_POP
                ]),
                WriteSimulate([.. elementList.Select(x => new XElement(x))], [
                    COM_PUSH, 0, 1, 0, COM_POP
                ])
            );
        }

        [TestMethod]
        public void 遅延書き込みの評価順序の確認()
        {
            // 検査対象のXELemenet
            List<XElement> elementList = [
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "あ")),
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "い")),
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "う"))
            ];
            // 書き込みの検査
            CollectionAssert.AreEqual(
                WriteSimulate([.. elementList.Select(x => new XElement(x))], [
                    COM_PUSH, 0, 1, 2, COM_WRITE_CHUNK, COM_POP
                ]),
                WriteSimulate([.. elementList.Select(x => new XElement(x))], [
                    COM_PUSH, 0, 1, 2, 2, 1, 0, COM_POP
                ])
            );
        }

        [TestMethod]
        public void 多段の遅延書き込みの評価順序の確認()
        {
            // 検査対象のXELemenet
            List<XElement> elementList = [
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "あ")),
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "い")),
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "う")),
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "え")),
                 new XElement("item", new XAttribute("eval", "lazy"), new XElement("value", "お"))
            ];
            // 書き込みの検査
            CollectionAssert.AreEqual(
                WriteSimulate([.. elementList.Select(x => new XElement(x))], [
                    COM_PUSH, 0, COM_PUSH, 1, COM_PUSH, 2, COM_WRITE_CHUNK, COM_POP, 3, COM_WRITE_CHUNK, COM_POP, 4, COM_WRITE_CHUNK, COM_POP
                ]),
                WriteSimulate([.. elementList.Select(x => new XElement(x))], [
                    COM_PUSH, 0, 1, 2, 2, 3, 3, 1, 4, 4, 0, COM_POP
                ])
            );
        }
    }
}