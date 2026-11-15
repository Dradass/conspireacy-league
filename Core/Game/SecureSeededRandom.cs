using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Источник случайности партии на сервере: криптостойкий и при этом воспроизводимый по секретному ключу.
/// </summary>
/// <remarks>
/// <para>
/// Обычный <see cref="Random"/> с числовым seed нельзя использовать в сетевой игре: вариантов seed всего 2³¹, и,
/// увидев несколько бросков кубика и карточек рынка, игрок переберёт их и узнает порядок колоды и будущие броски.
/// </para>
/// <para>
/// Здесь поток случайных байт — HMAC-SHA256 от 256-битного секретного ключа и номера блока. Без ключа он
/// неотличим от случайного, а с тем же ключом повторяется точь-в-точь — так сервер может заново проиграть партию
/// по журналу команд (<see cref="Server.ServerGame"/>). Ключ никогда не покидает сервер.
/// </para>
/// </remarks>
public sealed class SecureSeededRandom : Random
{
  /// <summary>
  /// Длина ключа, байт.
  /// </summary>
  public const int KeyLength = 32;

  private readonly byte[] _key;
  private readonly byte[] _block = new byte[32];
  private readonly byte[] _counterBytes = new byte[8];
  private ulong _counter;
  private int _position;

  /// <param name="key">Секретный ключ длиной <see cref="KeyLength"/> байт.</param>
  public SecureSeededRandom(ReadOnlySpan<byte> key)
  {
    if (key.Length != KeyLength)
      throw new ArgumentException($"Ключ должен быть длиной {KeyLength} байт.", nameof(key));
    _key = key.ToArray();
    _position = _block.Length;
  }

  /// <summary>
  /// Новый случайный секретный ключ.
  /// </summary>
  public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeyLength);

  public override int Next() => Next(int.MaxValue);

  public override int Next(int maxValue)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
    return maxValue <= 1 ? 0 : (int)Below((uint)maxValue);
  }

  public override int Next(int minValue, int maxValue)
  {
    ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
    var range = (ulong)((long)maxValue - minValue);
    return range <= 1 ? minValue : (int)(minValue + (long)Below(range));
  }

  public override long NextInt64() => NextInt64(long.MaxValue);

  public override long NextInt64(long maxValue)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
    return maxValue <= 1 ? 0 : (long)Below((ulong)maxValue);
  }

  public override long NextInt64(long minValue, long maxValue)
  {
    ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
    var range = (ulong)(maxValue - minValue);
    return range <= 1 ? minValue : minValue + (long)Below(range);
  }

  public override double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

  public override float NextSingle() => (NextUInt64() >> 40) * (1.0f / (1U << 24));

  protected override double Sample() => NextDouble();

  public override void NextBytes(byte[] buffer) => NextBytes(buffer.AsSpan());

  public override void NextBytes(Span<byte> buffer)
  {
    for (var i = 0; i < buffer.Length; i++)
      buffer[i] = NextByte();
  }

  /// <summary>
  /// Равномерно случайное число от 0 до <paramref name="range"/> (не включая) — без перекоса: числа из неполного
  /// последнего «круга» отбрасываются.
  /// </summary>
  private ulong Below(ulong range)
  {
    var limit = ulong.MaxValue - ulong.MaxValue % range;
    ulong value;
    do
      value = NextUInt64();
    while (value >= limit);
    return value % range;
  }

  private ulong NextUInt64()
  {
    Span<byte> bytes = stackalloc byte[8];
    for (var i = 0; i < bytes.Length; i++)
      bytes[i] = NextByte();
    return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
  }

  private byte NextByte()
  {
    if (_position == _block.Length)
    {
      BinaryPrimitives.WriteUInt64LittleEndian(_counterBytes, _counter++);
      HMACSHA256.HashData(_key, _counterBytes, _block);
      _position = 0;
    }

    return _block[_position++];
  }
}
