using System.Diagnostics.CodeAnalysis;

static class Extensions
{
    public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
    {
        stream.Write(buffer.ToArray(), 0, buffer.Length);
    }

    public static bool TryPeek<T>(this Stack<T> stack, [MaybeNullWhen(false)] out T result)
    {
        if (stack.Count > 0)
        {
            result = stack.Peek();
            return true;
        }
        result = default!;
        return false;
    }
}

namespace System.Runtime.CompilerServices
{
    static class RuntimeHelpers
    {
        public static T[] GetSubArray<T>(T[] array, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(array.Length);

            if (length == 0)
            {
                return [];
            }

            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
}
