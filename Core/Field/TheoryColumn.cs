using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;
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

  /// <summary>
  /// Владелец получает карточку теории, когда у теории хотя бы столько лидеров (карточек и миплов-лидеров).
  /// </summary>
  public const int TheoryCardLeadersThreshold = 3;

  /// <summary>
  /// Сколько единиц убеждения (<see cref="Influence"/>) нужно, чтобы теория была действующей.
  /// </summary>
  public const int ActiveInfluence = 4;

  /// <summary>
  /// Влияние мипла-лидера (<see cref="LeaderMeeples"/>) — как у профессионала, который его ставит (Бандита).
  /// </summary>
  public const int LeaderMeepleInfluence = GenericProfessionalCard.BanditInfluence;

  private readonly List<TheoryMember> _members = [];
  private readonly List<Player> _leaderMeeples = [];

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
  /// Миплы-лидеры под карточкой теории — чьего цвета, в порядке появления. Мипл считается лидером теории:
  /// даёт своему игроку влияние (<see cref="LeaderMeepleInfluence"/>) и долю дохода от адептов,
  /// но не учитывается при проверке состоятельности штаба. Миплов снимают после дохода от адептов.
  /// </summary>
  public IReadOnlyList<Player> LeaderMeeples => _leaderMeeples;

  /// <summary>
  /// Суммарное влияние лидеров теории: персон в стопке и миплов-лидеров.
  /// </summary>
  public int Influence => _members.Sum(member => member.Card.Influence) + _leaderMeeples.Count * LeaderMeepleInfluence + FounderInfluenceBonus;

  /// <summary>
  /// Добавка к влиянию основателя теории (пассивная способность «Плоской Земли»).
  /// </summary>
  internal int FounderInfluenceBonus { get; set; }

  /// <summary>
  /// Насколько труднее закрыть штаб: бездомных должно быть больше остальных на эту величину плюс один
  /// (пассивная способность «Антипрививочников»).
  /// </summary>
  internal int SolvencyMargin { get; set; }

  /// <summary>
  /// Владельцы, чьи бездомные в этом штабе влияют на закрытие за двух (пассивная способность «Коммунистов в Антарктиде»).
  /// </summary>
  internal IReadOnlyCollection<Player> DoubleHomelessOwners { get; set; } = [];

  /// <summary>
  /// Владельцы, чьи закрытые карточки в этом штабе считаются бездомными (пассивная способность «Иллюминатов»).
  /// </summary>
  internal IReadOnlyCollection<Player> IlluminatiHiddenOwners { get; set; } = [];

  /// <summary>
  /// Адептов в штабе нельзя перевести или убрать: на них не действуют никакие эффекты (шапочка из фольги).
  /// </summary>
  public bool AdeptsShielded { get; internal set; }

  /// <summary>
  /// Количество лидеров теории: карточки стопки и миплы-лидеры.
  /// </summary>
  public int Leaders => _members.Count + _leaderMeeples.Count;

  /// <summary>
  /// Количество адептов теории.
  /// </summary>
  public int Adepts { get; private set; }

  /// <summary>
  /// Штаб теории закрыт как несостоятельный: адепты не приходят и не приносят денег. Карточки в стопку класть
  /// по-прежнему можно — так штаб можно снова сделать состоятельным (см. <see cref="PlayingField.ReopenSolventTheories"/>).
  /// </summary>
  public bool IsClosed { get; private set; }

  /// <summary>
  /// Несостоятелен ли штаб: в нём больше <see cref="SolvencyCheckLeadersThreshold"/> лидеров,
  /// и бездомных среди них больше, чем остальных персонажей. Штабы с меньшим числом лидеров не проверяются.
  /// Миплы-лидеры (<see cref="LeaderMeeples"/>) здесь не учитываются.
  /// </summary>
  public bool IsInsolvent
  {
    get
    {
      if (_members.Count <= SolvencyCheckLeadersThreshold)
        return false;

      // Бездомный владельца «Коммунистов в Антарктиде» считается за два.
      var homeless = _members.Where(CountsAsHomeless)
        .Sum(member => DoubleHomelessOwners.Contains(member.Owner) ? 2 : 1);
      var others = _members.Count(member => !CountsAsHomeless(member));
      // Каждый учёный в штабе делает закрытие сложнее (пассивная способность учёного в штабе теории).
      var scientists = _members.Count(member => member.Card.Passive is ScientistPassive);
      return homeless > others + SolvencyMargin + scientists * ScientistPassive.SolvencyBonus;
    }
  }

  /// <summary>
  /// Считается ли персонаж бездомным при проверке штаба: настоящий бездомный или закрытая карточка владельца «Иллюминатов».
  /// </summary>
  private bool CountsAsHomeless(TheoryMember member) =>
    member.Card is HomelessCard || member.Card is HiddenCard && IlluminatiHiddenOwners.Contains(member.Owner);

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
    .Concat(_leaderMeeples)
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
  /// Лидер ли игрок в теории: у него есть карточка в стопке или мипл-лидер под карточкой теории (<see cref="LeaderMeeples"/>).
  /// </summary>
  public bool IsLeader(Player player) => MembersOf(player).Any() || _leaderMeeples.Contains(player);

  /// <summary>
  /// Влияние игрока в стопке — сумма очков влияния выложенных им карточек и его миплов-лидеров.
  /// </summary>
  public int InfluenceOf(Player player) =>
    MembersOf(player).Sum(member => member.Card.Influence)
    + _leaderMeeples.Count(meeple => meeple == player) * LeaderMeepleInfluence
    + (player == Founder ? FounderInfluenceBonus : 0);

  /// <summary>
  /// Защищена ли теория полицейским: в стопке лежит открытый полицейский (<see cref="PolicemanPassive"/>).
  /// К защищённой теории нельзя применять негативные эффекты.
  /// </summary>
  /// <param name="except">Карточка, которая сейчас действует: сама себя она не защищает от своего действия.</param>
  public bool IsProtected(Cell? except = null) =>
    _members.Any(member => member.Cell != except && member.Card.Passive is PolicemanPassive);

  internal void Add(TheoryMember member) => _members.Add(member);

  /// <summary>
  /// Заменить карточку в ячейке стопки; владелец остаётся прежним.
  /// </summary>
  internal void Replace(Cell cell, PersonCard card, Player? owner = null)
  {
    var index = IndexOf(cell);
    _members[index] = _members[index] with { Card = card, Owner = owner ?? _members[index].Owner };
  }

  /// <summary>
  /// Убрать карточку из стопки: карточки над ней опускаются на одну ячейку.
  /// </summary>
  /// <returns>Переезды карточек снизу вверх: откуда и куда.</returns>
  internal IReadOnlyList<(Cell From, Cell To)> Remove(Cell cell)
  {
    var index = IndexOf(cell);
    if (_members.Count == 1)
      throw new InvalidOperationException($"Нельзя убрать единственную карточку теории «{Theory}».");

    var cells = _members.Select(member => member.Cell).ToList();
    var moves = new List<(Cell From, Cell To)>();
    for (var i = index + 1; i < _members.Count; i++)
    {
      moves.Add((cells[i], cells[i - 1]));
      _members[i] = _members[i] with { Cell = cells[i - 1] };
    }

    _members.RemoveAt(index);
    return moves;
  }

  /// <summary>
  /// Сдвинуть всю стопку по горизонтали на <paramref name="dx"/> столбцов; ряды карточек не меняются.
  /// </summary>
  internal void Shift(int dx)
  {
    for (var i = 0; i < _members.Count; i++)
      _members[i] = _members[i] with { Cell = _members[i].Cell with { Column = _members[i].Cell.Column + dx } };
  }

  /// <summary>
  /// Поменять местами карточки двух ячеек стопки вместе с их владельцами.
  /// </summary>
  internal void Swap(Cell first, Cell second)
  {
    var i = IndexOf(first);
    var j = IndexOf(second);
    var a = _members[i];
    var b = _members[j];
    _members[i] = a with { Card = b.Card, Owner = b.Owner };
    _members[j] = b with { Card = a.Card, Owner = a.Owner };
  }

  private int IndexOf(Cell cell)
  {
    var index = _members.FindIndex(member => member.Cell == cell);
    return index >= 0 ? index : throw new ArgumentException($"Ячейка {cell} не относится к теории «{Theory}».", nameof(cell));
  }

  internal void AddAdepts(int count) => Adepts += count;

  internal void RemoveAdepts(int count) => Adepts -= count;

  internal void AddLeaderMeeple(Player player) => _leaderMeeples.Add(player);

  internal void ClearLeaderMeeples() => _leaderMeeples.Clear();

  internal void Close() => IsClosed = true;

  /// <summary>
  /// Штаб снова состоятелен: его открывают обратно (см. <see cref="PlayingField.ReopenSolventTheories"/>).
  /// </summary>
  internal void Reopen() => IsClosed = false;

  public override string ToString() => Theory.Name;
}
