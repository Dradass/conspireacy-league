namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Контекст партии, в котором выполняются эффекты карт.
/// </summary>
/// <remarks>
/// TODO: состояние игры — игроки, их деньги, рука, теория, штаб теории, адепты,
/// а также источник случайности и способ запросить выбор у игрока.
/// </remarks>
public sealed class GameContext
{
  /// <summary>
  /// Игрок, для которого выполняются эффекты.
  /// </summary>
  public Player CurrentPlayer { get; }

  /// <param name="currentPlayer">Игрок, для которого выполняются эффекты.</param>
  public GameContext(Player currentPlayer)
  {
    ArgumentNullException.ThrowIfNull(currentPlayer);
    CurrentPlayer = currentPlayer;
  }
}
