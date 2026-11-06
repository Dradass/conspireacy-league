namespace ConspiracyLeague.Core.Decks;

/// <summary>
/// Вид персонажа в составе игры: бездомный, гражданин или профессионал (его профессия задаётся отдельно).
/// </summary>
public enum PersonKind
{
  /// <summary>
  /// Бездомный.
  /// </summary>
  Homeless,

  /// <summary>
  /// Гражданин.
  /// </summary>
  Citizen,

  /// <summary>
  /// Профессионал.
  /// </summary>
  Professional,
}
