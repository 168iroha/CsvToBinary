using System.Numerics;

namespace CsvToBinary.Xml
{

    /// <summary>
    /// 文字列を指定した方式でバイナリへ変換するためのクラス
    /// </summary>
    public class ConvertToBytes
    {
        /// <summary>
        /// 2進数文字列をバイトデータに変換
        /// </summary>
        /// <param name="str">2進数文字列</param>
        /// <returns>変換結果のバイトデータ</returns>
        public static byte[] FromBinary(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return [];
            }
            int length = str.Length;
            int fullbyteLength = length / 8;
            byte[] byteArray = new byte[(length + 7) / 8];

            int i = 0;
            for (; i < fullbyteLength; ++i)
            {
                // 8文字づつ切り出して値を設定する
                byteArray[i] = Convert.ToByte(str.Substring(i * 8, 8), 2);
            }
            if (i < byteArray.Length)
            {
                // フル桁設定されていないデータの設定
                byteArray[i] = Convert.ToByte(str[(i * 8)..length], 2);
            }

            return byteArray;
        }

        /// <summary>
        /// 16進数文字列をバイトデータに変換
        /// </summary>
        /// <param name="str">16進数文字列</param>
        /// <returns>変換結果のバイトデータ</returns>
        public static byte[] FromHexadecimal(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return [];
            }
            int length = str.Length;
            int fullbyteLength = length / 2;
            byte[] byteArray = new byte[(length + 1) / 2];

            int i = 0;
            for (; i < fullbyteLength; ++i)
            {
                // 2文字づつ切り出して値を設定する
                byteArray[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
            }
            if (i < byteArray.Length)
            {
                // フル桁設定されていないデータの設定
                byteArray[i] = Convert.ToByte(str[(i * 2)..length], 16);
            }

            return byteArray;
        }

        /// <summary>
        /// 10進数文字列をバイトデータに変換(出力バイト数チェックは行わない)
        /// </summary>
        /// <param name="str">10進数文字列</param>
        /// <param name="bytes">出力バイト数(0の場合は最小バイト数で出力)</param>
        /// <returns>変換結果のバイトデータ</returns>
        public static byte[] FromDecimal(string str, int bytes = 0)
        {
            if (string.IsNullOrEmpty(str))
            {
                return [];
            }
            var number = BigInteger.Parse(str);
            var bytesArray = number.ToByteArray();
            if (bytesArray.Length < bytes)
            {
                // バイトが少なければ適宜補間
                byte padding = (byte)(number < 0 ? 0xFF : 0);
                var byteArray2 = new byte[bytes];
                Array.Copy(bytesArray, byteArray2, bytesArray.Length);
                for (int i = bytesArray.Length; i < bytes; ++i)
                {
                    byteArray2[i] = padding;
                }
                return byteArray2;
            }
            return bytesArray;
        }
    }
}
