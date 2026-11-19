using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Bots;

/// <summary>
/// Оценка ходов в режиме гонки.
/// </summary>
/// <remarks>
/// Очки в гонке дают только теории на финише: владельцу — <see cref="RaceScoring.FinishBonus"/>, каждому лидеру — доля
/// адептов. Как только одна теория доходит до финиша, раунд становится последним — остальные теории почти теряют шанс.
/// Поэтому бот прогнозирует, какие теории дойдут до финиша (<see cref="FinishChances"/>), и сколько очков там получит
/// каждый игрок (<see cref="RaceOutlook"/>). Ход ценится по тому, как он меняет этот прогноз: свои ожидаемые очки
/// минус ожидаемые очки соперников с их весами. Так бот гонит свою теорию к финишу (это ещё и отнимает шансы у теорий
/// соперников), входит лидером в чужую теорию, которая вот-вот финиширует, и не торопит чужую теорию к финишу, если
/// сам в ней почти ничего не получит.
/// </remarks>
internal sealed partial class BotEvaluator
{
  /// <summary>
  /// На сколько лидеров теория может отставать от ближайшей к финишу и всё же успеть (за последний раунд).
  /// </summary>
  private const double RaceSlack = 1.5;

  /// <summary>
  /// Насколько плавно шанс финиша падает с отставанием от ближайшей к финишу теории и от конца колоды (в лидерах).
  /// </summary>
  private const double RaceSlackSpread = 0.8;

  /// <summary>
  /// Во сколько лидеров отставания от финиша шанс теории обойти соперниц падает в <c>e</c> раз.
  /// </summary>
  private const double RaceSpread = 1.0;

  /// <summary>
  /// Сколько теорий, по прогнозу, доходит до финиша за партию: обычно первая и изредка ещё одна за последний раунд.
  /// </summary>
  private const double RaceFinishers = 1.2;

  /// <summary>
  /// Сколько лидеров за раунд может прийти в теорию, которую усиленно развивают.
  /// </summary>
  private const double RaceLeadersPerRound = 1.2;

  /// <summary>
  /// Больше скольких очков бот не ценит 1$ в гонке.
  /// </summary>
  private const double RaceMoneyCap = 0.25;

  /// <summary>
  /// Теория в прогнозе гонки: сколько лидеров и влияния у каждого игрока.
  /// </summary>
  /// <param name="Members">Карточек в стопке (миплы-лидеры при проверке штаба не считаются).</param>
  /// <param name="Slack">Запас состоятельности штаба (<see cref="TheoryColumn.SolvencySlack"/>).</param>
  private sealed record RaceTheory(
    int FinishLeaders,
    int Adepts,
    int Members,
    int Slack,
    IReadOnlyDictionary<Player, int> LeadersBy,
    IReadOnlyDictionary<Player, int> InfluenceBy,
    Player? Owner)
  {
    /// <summary>
    /// Штаб будет закрыт после раунда: несостоятельный закрывается, а закрытый, но состоятельный — открывается снова.
    /// </summary>
    public bool Closed => Members > TheoryColumn.SolvencyCheckLeadersThreshold && Slack < 0;

    public int Leaders => LeadersBy.Values.Sum();

    public int Influence => InfluenceBy.Values.Sum();

    public int Missing => FinishLeaders - Leaders;
  }

  private List<(TheoryColumn Column, RaceTheory Theory)>? _raceTheories;
  private Dictionary<Player, double>? _raceOutlook;

  private List<(TheoryColumn Column, RaceTheory Theory)> RaceTheories =>
    _raceTheories ??= Field.TheoryColumns.Select(column => (column, RaceTheoryOf(column))).ToList();

  /// <summary>
  /// Прогноз очков всех игроков по нынешнему полю.
  /// </summary>
  private Dictionary<Player, double> CurrentRaceOutlook => _raceOutlook ??= RaceOutlook(RaceTheories.Select(entry => entry.Theory).ToList());

  private RaceTheory RaceTheoryOf(TheoryColumn column) => new(
    RaceScoring.FinishLeaders(column, _session.Players.Count),
    column.Adepts,
    column.Members.Count,
    column.SolvencySlack,
    _session.Players.ToDictionary(player => player, player => LeadersOf(column, player)),
    _session.Players.ToDictionary(player => player, column.InfluenceOf),
    column.Owner);

  /// <summary>
  /// Теория, в которую пришёл ещё один лидер игрока с таким влиянием. Владельцем становится тот, у кого влияния больше;
  /// при равенстве владелец прежний.
  /// </summary>
  /// <param name="card">Лидер — карточка в стопке (а не мипл).</param>
  /// <param name="homeless">Карточка считается бездомным при проверке штаба.</param>
  private static RaceTheory WithLeader(RaceTheory theory, Player player, int influence, bool card = true, bool homeless = false)
  {
    var leaders = new Dictionary<Player, int>(theory.LeadersBy);
    leaders[player] = leaders.GetValueOrDefault(player) + 1;
    var influences = new Dictionary<Player, int>(theory.InfluenceBy);
    influences[player] = influences.GetValueOrDefault(player) + influence;
    var owner = theory.Owner is { } current && influences.GetValueOrDefault(current) >= influences[player] ? current : player;
    return theory with
    {
      LeadersBy = leaders,
      InfluenceBy = influences,
      Owner = owner,
      Members = theory.Members + (card ? 1 : 0),
      Slack = theory.Slack + (card ? homeless ? -1 : 1 : 0),
    };
  }

  /// <summary>
  /// Шансы теорий быть на финише к концу партии (в том же порядке).
  /// </summary>
  /// <remarks>
  /// Теория на финише (и действующая) — наверняка. Пока ни одна не дошла, теории соревнуются: до финиша обычно доходит
  /// одна (<see cref="RaceFinishers"/>), и шанс делится между открытыми теориями тем сильнее в пользу теории, чем меньше
  /// ей не хватает лидеров (каждый лидер — в <c>e</c> раз на <see cref="RaceSpread"/> лидеров). Ещё теория должна успеть до
  /// конца колоды рынка. Когда одна теория дошла (или раунд последний), успеют лишь те, кто отстаёт от ближайшей к
  /// финишу не больше чем на <see cref="RaceSlack"/> лидера.
  /// </remarks>
  private List<double> FinishChances(IReadOnlyList<RaceTheory> theories)
  {
    var open = theories.Where(theory => !theory.Closed).ToList();
    var front = open.Count == 0 ? RaceScoring.FinishLeaders(_session.Players.Count) : open.Min(theory => theory.Missing);
    var last = _session.IsLastRound || front <= 0;
    var weights = open.Sum(theory => Math.Exp(-theory.Missing / RaceSpread));
    return theories.Select(theory =>
    {
      if (theory.Closed)
        return 0;
      if (theory.Missing <= 0)
        return theory.Influence >= TheoryColumn.ActiveInfluence ? 1 : 0.5;
      if (last)
        return Sigmoid((Math.Max(front, 0) + RaceSlack - theory.Missing) / RaceSlackSpread);
      var lead = Math.Min(1, RaceFinishers * Math.Exp(-theory.Missing / RaceSpread) / weights);
      var reach = Sigmoid(((RoundsLeft - 0.5) * RaceLeadersPerRound - theory.Missing) / RaceSlackSpread);
      return lead * reach;
    }).ToList();
  }

  private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

  /// <summary>
  /// Сколько очков, по прогнозу, получит каждый игрок: по каждой теории — шанс финиша, умноженный на бонус владельцу
  /// и долю адептов (нынешних и тех, что ещё успеют прийти).
  /// </summary>
  private Dictionary<Player, double> RaceOutlook(IReadOnlyList<RaceTheory> theories)
  {
    var outlook = _session.Players.ToDictionary(player => player, _ => 0.0);
    var chances = FinishChances(theories);
    var last = _session.IsLastRound || theories.Any(theory => !theory.Closed && theory.Missing <= 0);
    // Адепты ещё придут, пока партия идёт.
    var growth = last ? 0 : Math.Min(3, RoundsLeft - 1);
    for (var i = 0; i < theories.Count; i++)
    {
      var theory = theories[i];
      var leaders = theory.Leaders;
      if (chances[i] <= 0 || leaders == 0)
        continue;
      var adepts = theory.Adepts + growth;
      foreach (var player in _session.Players)
      {
        var points = (theory.Owner == player ? RaceScoring.FinishBonus : 0) + adepts * theory.LeadersBy.GetValueOrDefault(player) / (double)leaders;
        outlook[player] += chances[i] * points;
      }
    }

    return outlook;
  }

  /// <summary>
  /// Ценность прогноза для бота: свои очки минус очки соперников с их весами.
  /// </summary>
  private double RaceWorth(Dictionary<Player, double> outlook) =>
    outlook[_player] - Rivals.Sum(rival => RivalWeightOf(rival) * outlook[rival]);

  /// <summary>
  /// Насколько изменится ценность прогноза, если теорию заменить другой (или добавить новую, если <paramref name="column"/> не задан).
  /// </summary>
  private double RaceDelta(TheoryColumn? column, RaceTheory changed)
  {
    var theories = RaceTheories.Where(entry => entry.Column != column).Select(entry => entry.Theory).Append(changed).ToList();
    return RaceWorth(RaceOutlook(theories)) - RaceWorth(CurrentRaceOutlook);
  }

  /// <summary>
  /// Гонка: выгода от того, что в теорию придёт ещё один лидер бота с таким влиянием.
  /// </summary>
  /// <param name="homeless">Карточка считается бездомным при проверке штаба: слишком много бездомных закроют штаб — и свою
  /// теорию так терять нельзя, а чужую, близкую к финишу, так можно остановить.</param>
  private double RaceJoinValue(TheoryColumn column, int influence, bool homeless)
  {
    var theory = RaceTheories.First(entry => entry.Column == column).Theory;
    var changed = WithLeader(theory, _player, influence, homeless: homeless);
    var value = RaceDelta(column, changed);
    if (!changed.Closed && column == RaceFocus)
      value += RaceFocusValue(changed);
    return value;
  }

  /// <summary>
  /// Главная теория бота в гонке — та, которую он доводит до финиша: среди теорий, которыми он владеет, — где больше
  /// всего его лидеров, а из равных — где меньше всего не хватает до финиша. <c>null</c> — своих теорий нет.
  /// </summary>
  private TheoryColumn? RaceFocus => _raceFocus ??= RaceTheories
    .Where(entry => entry.Theory.Owner == _player && !entry.Theory.Closed)
    .OrderByDescending(entry => entry.Theory.LeadersBy[_player])
    .ThenBy(entry => entry.Theory.Missing)
    .Select(entry => entry.Column)
    .FirstOrDefault();

  private TheoryColumn? _raceFocus;

  /// <summary>
  /// Прибавка за лидера в главной теории (<see cref="BotProfile.RaceFocus"/>): бот будет и дальше доводить её до финиша,
  /// поэтому каждый лидер — доля будущих очков на финише (бонус владельцу и адепты).
  /// </summary>
  private double RaceFocusValue(RaceTheory theory)
  {
    if (_profile.RaceFocus <= 0)
      return 0;
    var points = RaceScoring.FinishBonus + (theory.Adepts + Math.Min(3, RoundsLeft - 1)) * theory.LeadersBy[_player] / (double)theory.Leaders;
    return _profile.RaceFocus * points / theory.FinishLeaders;
  }

  /// <summary>
  /// Гонка: выгода от новой теории, основанной ботом.
  /// </summary>
  private double RaceNewTheoryValue(int influence)
  {
    var theory = new RaceTheory(
      RaceScoring.FinishLeaders(_session.Players.Count),
      0,
      1,
      1,
      _session.Players.ToDictionary(player => player, player => player == _player ? 1 : 0),
      _session.Players.ToDictionary(player => player, player => player == _player ? influence : 0),
      _player);
    // Своих теорий нет — новая станет главной.
    return RaceDelta(null, theory) + (RaceFocus is null ? RaceFocusValue(theory) : 0);
  }

  /// <summary>
  /// Гонка: шанс, что теория будет на финише к концу партии.
  /// </summary>
  private double RaceFinishChance(TheoryColumn column)
  {
    _raceChances ??= FinishChances(RaceTheories.Select(entry => entry.Theory).ToList());
    return _raceChances[RaceTheories.FindIndex(entry => entry.Column == column)];
  }

  private List<double>? _raceChances;

  /// <summary>
  /// Гонка: мипл-лидер останется в теории до итогового подсчёта, только если раунд последний — уже или потому, что
  /// мипл доводит теорию до финиша. Иначе — <c>null</c>: мипл снимут при обновлении рынка.
  /// </summary>
  private double? RaceMeepleValue(TheoryColumn column)
  {
    var theory = RaceTheories.First(entry => entry.Column == column).Theory;
    if (!_session.IsLastRound && theory.Missing > 1)
      return null;
    return RaceDelta(column, WithLeader(theory, _player, TheoryColumn.LeaderMeepleInfluence, card: false));
  }
}
