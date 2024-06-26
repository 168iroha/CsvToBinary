﻿using System.Xml.Linq;
using Xml;

namespace Data
{
    /// <summary>
    /// バイナリデータの書き込みに関するクラス
    /// </summary>
    public class BinaryWriter : IDataWriter
    {
        /// <summary>
        /// 書き込み先のStream
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// XMLをStreamへ書き出す処理が記載されたインスタンス
        /// </summary>
        private readonly IXmlToBinary xmlToBinary;

        // 遅延評価のためのスタック
        private readonly Stack<Stack<XElement>> lazyEvalStack = [];

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="stream">書き込み先のStream</param>
        /// <param name="xmlToBinary">XMLをStreamへ書き出す処理が記載されたインスタンス</param>
        public BinaryWriter(Stream stream, IXmlToBinary xmlToBinary)
        {
            this.stream = stream;
            this.xmlToBinary = xmlToBinary;
        }

        /// <summary>
        /// 処理単位のデータを書き込む
        /// </summary>
        public void WriteChunk() {
            // 遅延評価の実行
            var stack = this.lazyEvalStack.Peek();
            while (stack.Count > 0)
            {
                this.xmlToBinary.Write(this.stream, stack.Pop());
            }
        }

        /// <summary>
        /// 書き込んだChunkをスタックにPushする
        /// </summary>
        public void Push() {
            this.lazyEvalStack.Push([]);
        }

        /// <summary>
        /// 書き込んだChunkをスタックからPopする
        /// </summary>
        public void Pop() {
            this.lazyEvalStack.Pop();
        }

        /// <summary>
        /// Chunkに関するスタックの深さの取得
        /// </summary>
        public int Depth()
        {
            return this.lazyEvalStack.Count;
        }

        /// <summary>
        /// XMLに従ったデータの設定方法でデータを設定する
        /// </summary>
        /// <param name="key">itemの親までの項目に関するキー</param>
        /// <param name="item">設定対象のデータの記述子</param>
        /// <returns>取得したデータ</returns>
        public void SetData(string key, XElement item)
        {
            var offset = this.stream.Position;
            this.xmlToBinary.Write(this.stream, item);

            var eval = item.Attribute("eval")?.Value;
            if (eval == "lazy")
            {
                // itemを遅延評価するためにスタックに積む(書き込み領域の確保のためにWrite自体は実行する)
                item.SetAttributeValue("offset", offset);
                this.lazyEvalStack.Peek().Push(item);
            }
        }

        public void Dispose()
        {
            this.stream.Dispose();
        }
    }
}
