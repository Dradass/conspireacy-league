using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Сколько получает от адептов лидер теории — карточка стопки.
/// </summary>
/// <param name="Member">Карточка стопки и её владелец.</param>
/// <param name="Amount">Сумма в $.</param>
public sealed record MemberIncome(TheoryMember Member, int Amount);

/// <summary>
/// Сколько получает от адептов мипл-лидер теории (<see cref="TheoryColumn.LeaderMeeples"/>).
/// </summary>
/// <param name="Owner">Чей мипл.</param>
/// <param name="Index">Номер мипла в <see cref="TheoryColumn.LeaderMeeples"/>.</param>
/// <param name="Amount">Сумма в $.</param>
public sealed record MeepleIncome(Player Owner, int Index, int Amount);

/// <summary>
/// Доход теории от адептов: каждый адепт приносит 1$, деньги делятся поровну между лидерами теории
/// (карточками стопки и миплами-лидерами), остаток от деления достаётся владельцу штаба теории
/// (<see cref="TheoryColumn.Owner"/>).
/// </summary>
/// <param name="Column">Теория.</param>
/// <param name="Members">Доход каждой карточки стопки снизу вверх.</param>
/// <param name="Meeples">Доход каждого мипла-лидера.</param>
public sealed record TheoryIncome(TheoryColumn Column, IReadOnlyList<MemberIncome> Members, IReadOnlyList<MeepleIncome> Meeples)
{
  /// <summary>
  /// Весь доход теории.
  /// </summary>
  public int Total => Members.Sum(member => member.Amount) + Meeples.Sum(meeple => meeple.Amount);

  /// <summary>
  /// Сколько получает каждый игрок.
  /// </summary>
  public IEnumerable<(Player Player, int Amount)> ByPlayer =>
    Members.Select(member => (member.Member.Owner, member.Amount))
      .Concat(Meeples.Select(meeple => (meeple.Owner, meeple.Amount)))
      .GroupBy(income => income.Owner)
      .Select(group => (group.Key, group.Sum(income => income.Amount)));

  /// <summary>
  /// Посчитать доход теории.
  /// </summary>
  public static TheoryIncome Of(TheoryColumn column)
  {
    var leaders = column.Members.Count + column.LeaderMeeples.Count;
    var share = column.Adepts / leaders;
    var remainder = column.Adepts % leaders;
    // Остаток кладём на первую карточку владельца штаба (а если у него только миплы — на первого мипла):
    // так он виден на конкретном лидере.
    var owner = column.Owner;
    var ownerCard = column.MembersOf(owner).FirstOrDefault();
    var ownerMeeple = ownerCard is null ? column.LeaderMeeples.ToList().IndexOf(owner) : -1;
    var members = column.Members
      .Select(member => new MemberIncome(member, share + (member == ownerCard ? remainder : 0)))
      .ToList();
    var meeples = column.LeaderMeeples
      .Select((player, index) => new MeepleIncome(player, index, share + (index == ownerMeeple ? remainder : 0)))
      .ToList();
    return new TheoryIncome(column, members, meeples);
  }
}
