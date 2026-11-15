using System.Text.Json.Serialization;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Views;

/// <summary>
/// Что произошло на столе — для анимаций у клиента. Сервер присылает события вместе с новым видом партии
/// (<see cref="ServerUpdate"/>): клиент проигрывает события по порядку, а потом показывает вид.
/// </summary>
/// <remarks>
/// События одинаковы для всех игроков, поэтому в них только то, что видно всем: ячейки, места за столом, названия
/// теорий и суммы. Карточек и их идентификаторов в событиях нет — что лежит в ячейке, клиент берёт из вида партии
/// (<see cref="PlayerView"/>), где скрытое уже отсечено.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StateReset), "stateReset")]
[JsonDerivedType(typeof(PlayerActed), "playerActed")]
[JsonDerivedType(typeof(TurnStarted), "turnStarted")]
[JsonDerivedType(typeof(FirstPlayerChanged), "firstPlayerChanged")]
[JsonDerivedType(typeof(PlayerPassed), "playerPassed")]
[JsonDerivedType(typeof(PlayerAutoPassed), "playerAutoPassed")]
[JsonDerivedType(typeof(PhaseChanged), "phaseChanged")]
[JsonDerivedType(typeof(LastRoundDeclared), "lastRoundDeclared")]
[JsonDerivedType(typeof(MoneyChanged), "moneyChanged")]
[JsonDerivedType(typeof(HandCountChanged), "handCountChanged")]
[JsonDerivedType(typeof(CardBought), "cardBought")]
[JsonDerivedType(typeof(CardPlaced), "cardPlaced")]
[JsonDerivedType(typeof(CardPlacedInWall), "cardPlacedInWall")]
[JsonDerivedType(typeof(HiddenCardPlaced), "hiddenCardPlaced")]
[JsonDerivedType(typeof(CardPlayed), "cardPlayed")]
[JsonDerivedType(typeof(DiceRolled), "diceRolled")]
[JsonDerivedType(typeof(CardEarned), "cardEarned")]
[JsonDerivedType(typeof(MarketCardOpened), "marketCardOpened")]
[JsonDerivedType(typeof(MarketCardTaken), "marketCardTaken")]
[JsonDerivedType(typeof(MarketCardDiscarded), "marketCardDiscarded")]
[JsonDerivedType(typeof(MarketCardMoved), "marketCardMoved")]
[JsonDerivedType(typeof(AdeptsAdded), "adeptsAdded")]
[JsonDerivedType(typeof(AdeptsRemoved), "adeptsRemoved")]
[JsonDerivedType(typeof(AdeptsMoved), "adeptsMoved")]
[JsonDerivedType(typeof(CardAdeptsAdded), "cardAdeptsAdded")]
[JsonDerivedType(typeof(TheoryClosed), "theoryClosed")]
[JsonDerivedType(typeof(TheoryReopened), "theoryReopened")]
[JsonDerivedType(typeof(TheoryMoved), "theoryMoved")]
[JsonDerivedType(typeof(CommonZoneGrew), "commonZoneGrew")]
[JsonDerivedType(typeof(CardsRefreshed), "cardsRefreshed")]
[JsonDerivedType(typeof(TheoryCardsRefreshed), "theoryCardsRefreshed")]
[JsonDerivedType(typeof(HiddenCardRevealed), "hiddenCardRevealed")]
[JsonDerivedType(typeof(TheoryEarned), "theoryEarned")]
[JsonDerivedType(typeof(LeaderMeepleAdded), "leaderMeepleAdded")]
[JsonDerivedType(typeof(LeaderMeeplesRemoved), "leaderMeeplesRemoved")]
[JsonDerivedType(typeof(CardTrained), "cardTrained")]
[JsonDerivedType(typeof(CardDestroyed), "cardDestroyed")]
[JsonDerivedType(typeof(CardDemoted), "cardDemoted")]
[JsonDerivedType(typeof(CardTakenToHand), "cardTakenToHand")]
[JsonDerivedType(typeof(CardRemovedFromTheory), "cardRemovedFromTheory")]
[JsonDerivedType(typeof(CardReplaced), "cardReplaced")]
[JsonDerivedType(typeof(CardRemoved), "cardRemoved")]
[JsonDerivedType(typeof(CardsSwapped), "cardsSwapped")]
[JsonDerivedType(typeof(TheoryCardGranted), "theoryCardGranted")]
[JsonDerivedType(typeof(TheoryCardTransferred), "theoryCardTransferred")]
[JsonDerivedType(typeof(TheoryCardUsed), "theoryCardUsed")]
[JsonDerivedType(typeof(CharacterChipped), "characterChipped")]
[JsonDerivedType(typeof(WheelMarked), "wheelMarked")]
[JsonDerivedType(typeof(TheoryIncomePaid), "theoryIncomePaid")]
[JsonDerivedType(typeof(LeadersBonusPaid), "leadersBonusPaid")]
public abstract record TableEvent;

/// <summary>
/// Переезд карточки из ячейки в ячейку.
/// </summary>
public sealed record CellMove(Cell From, Cell To);

/// <summary>
/// Сколько $ получил игрок.
/// </summary>
public sealed record SeatAmount(int Seat, int Amount);

/// <summary>
/// Партию на сервере вернули к прежнему состоянию (после внутренней ошибки): анимировать нечего, нужно просто
/// показать новый вид партии.
/// </summary>
public sealed record StateReset : TableEvent;

/// <summary>
/// Игрок сделал действие; текст — для истории партии (<see cref="Logging.ActionText"/>), без скрытого.
/// </summary>
public sealed record PlayerActed(int Seat, string Text) : TableEvent;

public sealed record TurnStarted(int Seat) : TableEvent;

public sealed record FirstPlayerChanged(int Seat) : TableEvent;

public sealed record PlayerPassed(int Seat) : TableEvent;

/// <summary>
/// Игроку нечем ходить — он спасовал автоматически.
/// </summary>
public sealed record PlayerAutoPassed(int Seat) : TableEvent;

public sealed record PhaseChanged(GamePhase Phase) : TableEvent;

/// <summary>
/// Режим гонки: теория дошла до финиша, этот раунд последний.
/// </summary>
public sealed record LastRoundDeclared(string Theory) : TableEvent;

public sealed record MoneyChanged(int Seat, int Money) : TableEvent;

public sealed record HandCountChanged(int Seat, int Count) : TableEvent;

public sealed record CardBought(int Seat, Cell Cell) : TableEvent;

/// <summary>
/// Игрок выложил карточку с руки в ячейку (теория потом может сдвинуться — см. <see cref="TheoryMoved"/>).
/// </summary>
public sealed record CardPlaced(int Seat, Cell Cell) : TableEvent;

/// <summary>
/// Игрок вставил дочернюю теорию в перегородку между столбцами.
/// </summary>
public sealed record CardPlacedInWall(int Seat, int Boundary) : TableEvent;

public sealed record HiddenCardPlaced(int Seat, Cell Cell) : TableEvent;

/// <summary>
/// Игрок играет карточку из личного штаба; дальше — бросок (<see cref="DiceRolled"/>).
/// </summary>
public sealed record CardPlayed(int Seat, Cell Cell, string Action) : TableEvent;

public sealed record DiceRolled(int Seat, Cell Cell, int Chance, int Die, bool Succeeded) : TableEvent;

/// <summary>
/// Карточка личного штаба принесла деньги: само действие, бонус учёных и бонус теорий.
/// </summary>
public sealed record CardEarned(Cell Cell, int Amount, int ScientistBonus, int TheoryBonus) : TableEvent;

public sealed record MarketCardOpened(Cell Cell) : TableEvent;

public sealed record MarketCardTaken(Cell Cell) : TableEvent;

public sealed record MarketCardDiscarded(Cell Cell) : TableEvent;

public sealed record MarketCardMoved(Cell From, Cell To) : TableEvent;

public sealed record AdeptsAdded(string Theory, int Count) : TableEvent;

public sealed record AdeptsRemoved(string Theory, int Count) : TableEvent;

public sealed record AdeptsMoved(string From, string To, int Count) : TableEvent;

public sealed record CardAdeptsAdded(Cell Cell, int Count) : TableEvent;

public sealed record TheoryClosed(string Theory) : TableEvent;

public sealed record TheoryReopened(string Theory) : TableEvent;

public sealed record TheoryMoved(string Theory, IReadOnlyList<CellMove> Moves) : TableEvent;

public sealed record CommonZoneGrew : TableEvent;

public sealed record CardsRefreshed(IReadOnlyList<Cell> Cells) : TableEvent;

public sealed record TheoryCardsRefreshed : TableEvent;

/// <summary>
/// Закрытую карту раскрыли — её лицевая сторона теперь в виде партии у всех.
/// </summary>
public sealed record HiddenCardRevealed(Cell Cell) : TableEvent;

/// <summary>
/// Карточка теории принесла деньги владельцу.
/// </summary>
public sealed record TheoryEarned(string Theory, int Seat, int Amount) : TableEvent;

public sealed record LeaderMeepleAdded(string Theory, int Seat) : TableEvent;

public sealed record LeaderMeeplesRemoved(IReadOnlyList<string> Theories) : TableEvent;

public sealed record CardTrained(Cell Cell) : TableEvent;

/// <summary>
/// Бездомный провалил проверку на низкой грани и ушёл из игры.
/// </summary>
public sealed record CardDestroyed(Cell Cell) : TableEvent;

/// <summary>
/// Гражданин провалил проверку на грани 1 и стал бездомным.
/// </summary>
public sealed record CardDemoted(Cell Cell) : TableEvent;

public sealed record CardTakenToHand(Cell Cell) : TableEvent;

public sealed record CardRemovedFromTheory(Cell Cell, IReadOnlyList<CellMove> Moves) : TableEvent;

public sealed record CardReplaced(Cell Cell) : TableEvent;

public sealed record CardRemoved(Cell Cell) : TableEvent;

public sealed record CardsSwapped(Cell First, Cell Second) : TableEvent;

public sealed record TheoryCardGranted(string Theory, int Seat) : TableEvent;

public sealed record TheoryCardTransferred(string Theory, int Seat) : TableEvent;

public sealed record TheoryCardUsed(string Theory, int Seat) : TableEvent;

public sealed record CharacterChipped(Cell Cell) : TableEvent;

public sealed record WheelMarked(Cell Cell) : TableEvent;

/// <summary>
/// Адепты теории принесли деньги лидерам (в конце раунда или в итоговом подсчёте).
/// </summary>
public sealed record TheoryIncomePaid(string Theory, int Total, IReadOnlyList<SeatAmount> BySeat) : TableEvent;

/// <summary>
/// Итоговый подсчёт: владелец действующей теории получил деньги за лидеров.
/// </summary>
public sealed record LeadersBonusPaid(string Theory, int Seat, int Amount) : TableEvent;

/// <summary>
/// Сообщение сервера игроку после каждого изменения партии: что произошло и как партия выглядит теперь.
/// </summary>
/// <param name="Version">Версия партии (<see cref="Server.ServerGame.Version"/>).</param>
/// <param name="Events">События по порядку — для анимаций.</param>
/// <param name="View">Партия глазами этого игрока после событий.</param>
public sealed record ServerUpdate(long Version, IReadOnlyList<TableEvent> Events, PlayerView View);
