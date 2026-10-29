using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Стопка карточек одной теории в общей зоне. Растёт снизу вверх.
/// </summary>
public sealed class TheoryColumn
{
  /// <summary>
  /// Штаб проверяется на состоятельность, только если лидеров (карточек в стопке) больше этого числа.
  /// </summary>
  public const int SolvencyCheckLeadersThreshold = 3;

  private readonly List<TheoryMember> _members = [];

  /// <summary>
  /// Теория.
  /// </summary>
  public Theory Theory { get; }

  /// <summary>
  /// Является ли стопка штабом теории. Штаб появляется, когда выложена родительская теория.
  /// </summary>
  public bool IsHeadquarters => !Theory.IsChild;

  /// <summary>
  /// Карточки стопки снизу вверх: где лежат и кто их выложил.
  /// </summary>
  public IReadOnlyList<TheoryMember> Members => _members;

  /// <summary>
  /// Ячейки стопки снизу вверх.
  /// </summary>
  public IReadOnlyList<Cell> Cells => _members.Select(member => member.Cell).ToList();

  /// <summary>
  /// Карточки стопки снизу вверх.
  /// </summary>
  public IReadOnlyList<PersonCard> Cards => _members.Select(member => member.Card).ToList();

  /// <summary>
  /// Ячейка последней (верхней) карточки.
  /// </summary>
  public Cell Top => _members[^1].Cell;

  /// <summary>
  /// Суммарное влияние персон в стопке.
  /// </summary>
  public int Influence => _members.Sum(member => member.Card.Influence);

  /// <summary>
  /// Количество адептов теории.
  /// </summary>
  public int Adepts { get; private set; }

  /// <summary>
  /// Штаб теории закрыт как несостоятельный: в стопку больше нельзя класть карточки, адепты не приходят
  /// и не приносят денег.
  /// </summary>
  public bool IsClosed { get; private set; }

  /// <summary>
  /// Несостоятелен ли штаб: в нём больше <see cref="SolvencyCheckLeadersThreshold"/> лидеров,
  /// и бездомных среди них больше, чем остальных персонажей. Штабы с меньшим числом лидеров не проверяются.
  /// </summary>
  public bool IsInsolvent
  {
    get
    {
      if (_members.Count <= SolvencyCheckLeadersThreshold)
        return false;

      var homeless = _members.Count(member => member.Card is HomelessCard);
      return homeless > _members.Count - homeless;
    }
  }

  /// <summary>
  /// Ячейка карточки теории — нижней в стопке. На неё ставятся фигурки адептов.
  /// </summary>
  public Cell TheoryCell => _members[0].Cell;

  /// <summary>
  /// Игрок, выложивший карточку теории (основателя теории).
  /// </summary>
  public Player Founder => _members[0].Owner;

  /// <summary>
  /// Владелец теории — игрок с наибольшим влиянием в стопке (<see cref="InfluenceOf"/>).
  /// При равенстве влияния — тот, кто выложил свою первую карточку раньше; основатель теории выложил её первым.
  /// </summary>
  public Player Owner => _members
    .Select(member => member.Owner)
    // Distinct сохраняет порядок первых карточек игроков, а MaxBy при равенстве возвращает первого.
    .Distinct()
    .MaxBy(InfluenceOf)!;

  internal TheoryColumn(Theory theory, TheoryMember founder)
  {
    Theory = theory;
    Add(founder);
  }

  /// <summary>
  /// Владелец карточки в ячейке стопки.
  /// </summary>
  /// <returns>Игрок или <c>null</c>, если ячейка не относится к стопке.</returns>
  public Player? OwnerAt(Cell cell) => _members.FirstOrDefault(member => member.Cell == cell)?.Owner;

  /// <summary>
  /// Карточки стопки, выложенные игроком.
  /// </summary>
  public IEnumerable<TheoryMember> MembersOf(Player player) => _members.Where(member => member.Owner == player);

  /// <summary>
  /// Влияние игрока в стопке — сумма очков влияния выложенных им карточек.
  /// </summary>
  public int InfluenceOf(Player player) => MembersOf(player).Sum(member => member.Card.Influence);

  internal void Add(TheoryMember member) => _members.Add(member);

  internal void AddAdepts(int count) => Adepts += count;

  internal void Close() => IsClosed = true;

  public override string ToString() => Theory.Name;
}
