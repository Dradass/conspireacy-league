using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Сколько получает от адептов лидер теории — карточка стопки.
/// </summary>
/// <param name="Member">Карточка стопки и её владелец.</param>
/// <param name="Amount">Сумма в $.</param>
public sealed record MemberIncome(TheoryMember Member, int Amount);

/// <summary>
/// Доход теории от адептов: каждый адепт приносит 1$, деньги делятся поровну между лидерами теории
/// (карточками стопки), остаток от деления достаётся владельцу штаба теории (<see cref="TheoryColumn.Owner"/>).
/// </summary>
/// <param name="Column">Теория.</param>
/// <param name="Members">Доход каждой карточки стопки снизу вверх.</param>
public sealed record TheoryIncome(TheoryColumn Column, IReadOnlyList<MemberIncome> Members)
{
  /// <summary>
  /// Весь доход теории.
  /// </summary>
  public int Total => Members.Sum(member => member.Amount);

  /// <summary>
  /// Сколько получает каждый игрок.
  /// </summary>
  public IEnumerable<(Player Player, int Amount)> ByPlayer =>
    Members.GroupBy(member => member.Member.Owner).Select(group => (group.Key, group.Sum(member => member.Amount)));

  /// <summary>
  /// Посчитать доход теории.
  /// </summary>
  public static TheoryIncome Of(TheoryColumn column)
  {
    var leaders = column.Members.Count;
    var share = column.Adepts / leaders;
    var remainder = column.Adepts % leaders;
    // Остаток кладём на первую карточку владельца штаба: так он виден на конкретной карточке.
    var ownerCard = column.MembersOf(column.Owner).First();
    return new TheoryIncome(column, column.Members
      .Select(member => new MemberIncome(member, share + (member == ownerCard ? remainder : 0)))
      .ToList());
  }
}
