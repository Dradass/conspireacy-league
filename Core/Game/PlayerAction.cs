using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Вид действия игрока.
/// </summary>
public enum PlayerActionKind
{
  /// <summary>Купил карточку с рынка.</summary>
  Buy,

  /// <summary>Выложил карточку с руки.</summary>
  Place,

  /// <summary>Вставил дочернюю теорию в перегородку.</summary>
  PlaceInWall,

  /// <summary>Выложил карточку взакрытую.</summary>
  PlaceHidden,

  /// <summary>Выбрал цель для действия выложенной карточки (или отказался).</summary>
  ResolvePlacement,

  /// <summary>Сыграл карточку из личного штаба.</summary>
  PlayCard,

  /// <summary>Применил карточку теории.</summary>
  UseTheoryCard,

  /// <summary>Владелец основной теории выбрал карточку в руке.</summary>
  TriggerHandCard,

  /// <summary>Владелец основной теории выбрал теорию.</summary>
  TriggerTheory,

  /// <summary>Владелец основной теории выбрал карточку на поле.</summary>
  TriggerCell,

  /// <summary>Владелец основной теории отказался от её действия.</summary>
  SkipTrigger,

  /// <summary>Спасовал.</summary>
  Pass,
}

/// <summary>
/// Карточка на поле, как она лежала в момент действия: чья она и где — в чьём личном штабе или в штабе какой теории.
/// Запоминается до действия: после него карточка могла уйти с поля или переехать.
/// </summary>
/// <param name="Cell">Ячейка.</param>
/// <param name="Card">Карточка или <c>null</c>, если ячейка была пуста.</param>
/// <param name="Owner">Чья: владелец личного штаба или тот, кто выложил её в штаб теории.</param>
/// <param name="Theory">Штаб теории, где она лежала, или <c>null</c> — личный штаб.</param>
public sealed record CardRef(Cell Cell, Card? Card, Player? Owner, string? Theory);

/// <summary>
/// Теория в момент действия: название и владелец.
/// </summary>
public sealed record TheoryRef(string Name, Player Owner);

/// <summary>
/// Цели действия, запомненные до его выполнения.
/// </summary>
/// <param name="Target">Выбранная карточка на поле.</param>
/// <param name="SecondTarget">Вторая выбранная карточка.</param>
/// <param name="Borrowed">Чужая карточка, чьё действие играют (Бандит, Хакер и т. п.).</param>
/// <param name="Copied">Карточка, чьё действие копируют.</param>
/// <param name="AdeptCard">Карточка в личном штабе, куда приводят адептов.</param>
/// <param name="Player">Выбранный игрок.</param>
/// <param name="HandCard">Выбранная карточка в руке (её видит только владелец).</param>
/// <param name="SourceTheory">Теория, откуда (перевести адептов).</param>
/// <param name="Theories">Выбранные теории.</param>
public sealed record ActionTargets(
  CardRef? Target = null,
  CardRef? SecondTarget = null,
  CardRef? Borrowed = null,
  CardRef? Copied = null,
  CardRef? AdeptCard = null,
  Player? Player = null,
  Card? HandCard = null,
  TheoryRef? SourceTheory = null,
  IReadOnlyList<TheoryRef>? Theories = null)
{
  public static ActionTargets None { get; } = new();
}

/// <summary>
/// Действие игрока — для журнала и истории партии (<see cref="GameSession.ActionTaken"/>).
/// </summary>
/// <param name="Player">Кто действовал.</param>
/// <param name="Kind">Что сделал.</param>
/// <param name="MoneyBefore">Сколько у него было денег до действия.</param>
/// <param name="Card">Карточка, с которой действовал (купленная, выложенная, сыгранная, выбранная в руке).</param>
/// <param name="Cell">Где: ячейка рынка, куда выложена, какая сыграна.</param>
/// <param name="Theory">Теория, куда выложена карточка, или выбранная теория.</param>
/// <param name="Effect">Действие карточки.</param>
/// <param name="Targets">Цели действия.</param>
/// <param name="Check">Проверка успеха при розыгрыше.</param>
public sealed record PlayerAction(
  Player Player,
  PlayerActionKind Kind,
  int MoneyBefore,
  Card? Card = null,
  Cell? Cell = null,
  TheoryRef? Theory = null,
  string? Effect = null,
  ActionTargets? Targets = null,
  SuccessCheck? Check = null);
