using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Views;

/// <summary>
/// Партия глазами одного игрока — то, что сервер присылает ему после каждого изменения. Здесь только то, что этот
/// игрок вправе знать (<see cref="PlayerViews.For"/>): своя рука, открытые карточки, закрытые карты — рубашкой, кроме
/// своих, подсмотренных и раскрытых, у рынка — только число карточек в колоде и резерве.
/// </summary>
/// <param name="Viewer">Место игрока, для которого вид, или <c>null</c> — зритель (видит только общее).</param>
/// <param name="Version">Версия партии на сервере (<see cref="Server.ServerGame.Version"/>).</param>
/// <param name="Mode">Режим партии.</param>
/// <param name="Phase">Фаза.</param>
/// <param name="Round">Номер раунда.</param>
/// <param name="IsLastRound">Последний раунд гонки.</param>
/// <param name="CurrentSeat">Кто ходит.</param>
/// <param name="FirstSeat">Первый игрок раунда.</param>
/// <param name="ActionsLeft">Сколько действий осталось у того, кто ходит.</param>
/// <param name="CommonZone">Общая зона поля (растёт с теориями).</param>
/// <param name="FailureConsequences">Последствия провала проверки на низких гранях включены.</param>
/// <param name="Players">Игроки по местам за столом.</param>
/// <param name="Hand">Своя рука; у зрителя пустая.</param>
/// <param name="Cards">Карточки на поле: в личных штабах и в штабах теорий.</param>
/// <param name="Theories">Штабы теорий.</param>
/// <param name="TheoryCards">Карточки теорий у владельцев.</param>
/// <param name="Market">Рынок.</param>
/// <param name="PendingCheck">Проверка успеха, которая сейчас идёт.</param>
/// <param name="PendingPlacement">Выбор цели для действия выложенной карточки.</param>
/// <param name="PendingTrigger">Выбор для действия основной теории.</param>
public sealed record PlayerView(
  int? Viewer,
  long Version,
  GameMode Mode,
  GamePhase Phase,
  int Round,
  bool IsLastRound,
  int CurrentSeat,
  int FirstSeat,
  int ActionsLeft,
  ZoneView CommonZone,
  bool FailureConsequences,
  IReadOnlyList<PlayerInfoView> Players,
  IReadOnlyList<CardView> Hand,
  IReadOnlyList<FieldCardView> Cards,
  IReadOnlyList<TheoryView> Theories,
  IReadOnlyList<TheoryCardView> TheoryCards,
  MarketView Market,
  SuccessCheckView? PendingCheck,
  PlacementView? PendingPlacement,
  TriggerView? PendingTrigger);

/// <summary>
/// Прямоугольная зона поля.
/// </summary>
public sealed record ZoneView(int Left, int Top, int Width, int Height);

/// <summary>
/// Карточка: идентификатор и лицевая сторона, если её видно.
/// </summary>
/// <param name="Id">Идентификатор (<see cref="GameSession.CardIds"/>).</param>
/// <param name="Hidden">Это закрытая карта (лежит рубашкой вверх).</param>
/// <param name="Face">Лицевая сторона; у закрытой карты — только для тех, кто её знает, иначе <c>null</c>.</param>
public sealed record CardView(int Id, bool Hidden, CardFace? Face);

/// <summary>
/// Игрок за столом. Чужую руку видно только как число карточек.
/// </summary>
/// <param name="EarnedFrom">
/// Откуда пришли деньги — только у самого игрока: в источниках бывают названия его закрытых карт.
/// </param>
public sealed record PlayerInfoView(
  int Seat,
  string Name,
  PlayerColor Color,
  bool IsBot,
  BotLevel BotLevel,
  int Money,
  int HandCount,
  bool Passed,
  IReadOnlyDictionary<string, int>? EarnedFrom);

/// <summary>
/// Карточка на поле.
/// </summary>
/// <param name="Cell">Ячейка.</param>
/// <param name="Card">Карточка.</param>
/// <param name="OwnerSeat">Чья карточка: владелец личного штаба или лидер в штабе теории.</param>
/// <param name="Theory">Штаб теории, где она лежит, или <c>null</c>, если в личном штабе.</param>
/// <param name="Influence">Влияние сейчас — у закрытой карты как у гражданина.</param>
/// <param name="SuccessChance">Вероятность успеха в этой ячейке, %.</param>
/// <param name="Trained">Изменение вероятности успеха обучением и ослаблением, %.</param>
/// <param name="Used">Сыграна (рубашкой вверх).</param>
/// <param name="Rotated">Повёрнута после быстрого действия.</param>
/// <param name="Adepts">Адепты на карточке в личном штабе.</param>
/// <param name="ChippedBy">Кто чипировал персонажа.</param>
/// <param name="WheelOf">Чей значок руля на карточке (вернётся к игроку, который спасовал).</param>
/// <param name="Foiled">На персонаже «Шапочка из фольги».</param>
/// <param name="Revealed">Закрытая карта раскрыта — её лицевую сторону видят все.</param>
public sealed record FieldCardView(
  Cell Cell,
  CardView Card,
  int OwnerSeat,
  string? Theory,
  int Influence,
  int SuccessChance,
  int Trained,
  bool Used,
  bool Rotated,
  int Adepts,
  int? ChippedBy,
  int? WheelOf,
  bool Foiled,
  bool Revealed);

/// <summary>
/// Штаб теории.
/// </summary>
public sealed record TheoryView(
  string Name,
  string? Parent,
  int OwnerSeat,
  int FounderSeat,
  bool Closed,
  bool Headquarters,
  bool Protected,
  bool AdeptsShielded,
  int Adepts,
  int Influence,
  int Leaders,
  bool Insolvent,
  IReadOnlyList<Cell> Members,
  IReadOnlyList<int> LeaderMeeples);

/// <summary>
/// Карточка теории у владельца.
/// </summary>
public sealed record TheoryCardView(CardView Card, int OwnerSeat, Cell Cell, bool Used, Cell? AttachedTo);

/// <summary>
/// Рынок: открытые карточки; колода и резерв — только числом.
/// </summary>
public sealed record MarketView(IReadOnlyList<MarketCardView> Open, int DeckCount, int ReserveCount);

/// <summary>
/// Открытая карточка рынка.
/// </summary>
public sealed record MarketCardView(Cell Cell, CardView Card);

/// <summary>
/// Проверка успеха.
/// </summary>
public sealed record SuccessCheckView(Cell Cell, int Chance, int Die, bool Succeeded);

/// <summary>
/// Выбор цели для действия выложенной карточки. Подходящие цели — только тому, кто выбирает.
/// </summary>
public sealed record PlacementView(
  Cell Cell,
  string Theory,
  string Effect,
  bool TargetIsOptional,
  bool RequiresSecondTarget,
  IReadOnlyList<Cell>? AllowedTargets);

/// <summary>
/// Выбор для действия основной теории. Подходящие цели и выбранная карточка в руке — только владельцу.
/// </summary>
/// <param name="ParentCardId">Карточка основной теории.</param>
/// <param name="OwnerSeat">Кто выбирает — владелец основной теории.</param>
/// <param name="Effect">Действие.</param>
/// <param name="NeedsHandCard">Нужна карточка в руке.</param>
/// <param name="NeedsTheory">Нужна теория.</param>
/// <param name="NeedsCell">Нужна карточка на поле.</param>
/// <param name="HandCardChosen">Карточка в руке уже выбрана.</param>
/// <param name="HandCardId">Выбранная карточка в руке — только владельцу.</param>
/// <param name="ChosenTheory">Выбранная теория.</param>
/// <param name="ChosenCell">Выбранная карточка на поле.</param>
/// <param name="AllowedHandCards">Какие карточки в руке подходят — только владельцу.</param>
/// <param name="AllowedTheories">Какие теории подходят — только владельцу.</param>
/// <param name="AllowedCells">Какие карточки на поле подходят — только владельцу.</param>
public sealed record TriggerView(
  int ParentCardId,
  int OwnerSeat,
  string Effect,
  bool NeedsHandCard,
  bool NeedsTheory,
  bool NeedsCell,
  bool HandCardChosen,
  int? HandCardId,
  string? ChosenTheory,
  Cell? ChosenCell,
  IReadOnlyList<int>? AllowedHandCards,
  IReadOnlyList<string>? AllowedTheories,
  IReadOnlyList<Cell>? AllowedCells);
