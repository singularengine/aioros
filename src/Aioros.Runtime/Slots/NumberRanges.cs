namespace Aioros.Runtime.Slots;

/// <summary>The values each integral type holds, so a posted number is clamped to its property's type before it is converted.</summary>
public static class NumberRanges
{
    /// <summary>The number clamped to what the type holds; unchanged for a type that is not integral.</summary>
    public static decimal Clamp(decimal number, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var (least, most) = Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => (byte.MinValue, byte.MaxValue),
            TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue),
            TypeCode.Int16 => (short.MinValue, short.MaxValue),
            TypeCode.UInt16 => (ushort.MinValue, ushort.MaxValue),
            TypeCode.Int32 => (int.MinValue, int.MaxValue),
            TypeCode.UInt32 => (uint.MinValue, uint.MaxValue),
            TypeCode.Int64 => (long.MinValue, long.MaxValue),
            TypeCode.UInt64 => (ulong.MinValue, ulong.MaxValue),
            _ => (decimal.MinValue, decimal.MaxValue),
        };
        return Math.Clamp(number, least, most);
    }
}
