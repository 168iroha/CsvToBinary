using CsvToBinary.Xml;

namespace tests.Xml
{
    [TestClass]
    public class ConvertToBytesTests
    {
        [TestMethod]
        public void _2進数文字列をバイトデータに変換()
        {
            var str = "0110100011101011";
            var bytes = ConvertToBytes.FromBinary(str);

            // エンディアンの変換等もなしで最小の配列が返される
            Assert.AreEqual(bytes.Length, 2);
            Assert.AreEqual(bytes[0], 0b01101000);
            Assert.AreEqual(bytes[1], 0b11101011);
        }

        [TestMethod]
        public void フルで指定されていない2進数文字列をバイトデータに変換()
        {
            var str = "011";
            var bytes = ConvertToBytes.FromBinary(str);

            Assert.AreEqual(bytes.Length, 1);
            Assert.AreEqual(bytes[0], 0b011);
        }

        [TestMethod]
        public void _2進数文字列として不正なデータの指定()
        {
            var str = "01102";
            try
            {
                var bytes = ConvertToBytes.FromBinary(str);
                Assert.Fail();
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void _16進数文字列をバイトデータに変換()
        {
            var str = "0F1B";
            var bytes = ConvertToBytes.FromHexadecimal(str);

            // エンディアンの変換等もなしで最小の配列が返される
            Assert.AreEqual(bytes.Length, 2);
            Assert.AreEqual(bytes[0], 0x0F);
            Assert.AreEqual(bytes[1], 0x1B);
        }

        [TestMethod]
        public void _10進数文字列をバイトデータに変換()
        {
            int number = 255;
            var str = $"{number}";
            var bytes = ConvertToBytes.FromDecimal(str);
            var targets = BitConverter.GetBytes(number);
            // バイト列の最小の長さの取得
            int length = 0;
            while (length < targets.Length && targets[length] != 0)
            {
                ++length;
            }
            // 符号ビットの分の補間
            if (length > 0 && (targets[length - 1] & 0b10000000) > 0)
            {
                ++length;
            }

            // エンディアンの変換等もなしで最小の配列が返される
            Assert.AreEqual(bytes.Length, length);
            CollectionAssert.AreEqual(bytes, targets[..length]);
        }

        [TestMethod]
        public void _10進数文字列をバイト数を指定してバイトデータに変換()
        {
            // 正の数を64ビット幅で出力
            long number1 = 255;
            var str1 = $"{number1}";
            var bytes1 = ConvertToBytes.FromDecimal(str1, 8);
            var targets1 = BitConverter.GetBytes(number1);
            Assert.AreEqual(bytes1.Length, 8);
            Assert.AreEqual(bytes1.Length, targets1.Length);
            CollectionAssert.AreEqual(bytes1, targets1);

            // 負の数を64ビット幅で出力
            long number2 = -255;
            var str2 = $"{number2}";
            var bytes2 = ConvertToBytes.FromDecimal(str2, 8);
            var targets2 = BitConverter.GetBytes(number2);
            Assert.AreEqual(bytes2.Length, 8);
            Assert.AreEqual(bytes2.Length, targets2.Length);
            CollectionAssert.AreEqual(bytes2, targets2);
        }

        [TestMethod]
        public void フルで指定されていない16進数文字列をバイトデータに変換()
        {
            var str = "C";
            var bytes = ConvertToBytes.FromHexadecimal(str);

            Assert.AreEqual(bytes.Length, 1);
            Assert.AreEqual(bytes[0], 0x0C);
        }

        [TestMethod]
        public void _16進数文字列として不正なデータの指定()
        {
            var str = "FG";
            try
            {
                var bytes = ConvertToBytes.FromHexadecimal(str);
                Assert.Fail();
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void 空データの変換()
        {
            Assert.AreEqual(ConvertToBytes.FromBinary("").Length, 0);
            Assert.AreEqual(ConvertToBytes.FromHexadecimal("").Length, 0);
            Assert.AreEqual(ConvertToBytes.FromDecimal("").Length, 0);
        }
    }
}
