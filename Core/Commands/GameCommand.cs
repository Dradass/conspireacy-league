using System.Text.Json.Serialization;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Commands;

/// <summary>
/// Действие игрока в виде, который можно передать по сети: на карточки, теории и игроков команда ссылается
/// идентификаторами, а не объектами. Сервер разбирает команду (<see cref="CommandProcessor"/>), проверяет и
/// выполняет её в своей партии.
/// </summary>
/// <remarks>
/// В команде нет игрока, который её прислал: его сервер знает по соединению, и подделать его в сообщении нельзя.
/// Карточки — по <see cref="Game.GameSession.CardIds"/>, теории — по названию (у каждой теории один штаб),
/// игроки — по месту за столом (индекс в <see cref="Game.GameSession.Players"/>), варианты действия — по индексу
/// в <see cref="Effects.Effect.Options"/> карточки.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BuyCommand), "buy")]
[JsonDerivedType(typeof(PlaceCommand), "place")]
[JsonDerivedType(typeof(PlaceInWallCommand), "placeInWall")]
[JsonDerivedType(typeof(PlaceHiddenCommand), "placeHidden")]
[JsonDerivedType(typeof(ResolvePlacementCommand), "resolvePlacement")]
[JsonDerivedType(typeof(UseCardCommand), "useCard")]
[JsonDerivedType(typeof(UseTheoryCardCommand), "useTheoryCard")]
[JsonDerivedType(typeof(SelectTriggerHandCardCommand), "triggerHandCard")]
[JsonDerivedType(typeof(SelectTriggerTheoryCommand), "triggerTheory")]
[JsonDerivedType(typeof(SelectTriggerCellCommand), "triggerCell")]
[JsonDerivedType(typeof(SkipTheoryTriggerCommand), "skipTrigger")]
[JsonDerivedType(typeof(PassCommand), "pass")]
public abstract record GameCommand;

/// <summary>
/// Купить карточку с рынка (<see cref="Game.GameSession.BuyFromMarket"/>).
/// </summary>
/// <param name="Cell">Ячейка рынка.</param>
public sealed record BuyCommand(Cell Cell) : GameCommand;

/// <summary>
/// Выложить карточку с руки на поле (<see cref="Game.GameSession.PlaceFromHand"/>).
/// </summary>
public sealed record PlaceCommand(int CardId, Cell Cell) : GameCommand;

/// <summary>
/// Вставить дочернюю теорию с руки в перегородку между столбцами (<see cref="Game.GameSession.PlaceInWallFromHand"/>).
/// </summary>
public sealed record PlaceInWallCommand(int CardId, int Boundary) : GameCommand;

/// <summary>
/// Выложить карточку с руки взакрытую в штаб теории (<see cref="Game.GameSession.PlaceHiddenFromHand"/>).
/// </summary>
/// <param name="Theory">Название теории.</param>
public sealed record PlaceHiddenCommand(int CardId, string Theory) : GameCommand;

/// <summary>
/// Выбрать цели для действия выложенной карточки или отказаться — обе цели <c>null</c>
/// (<see cref="Game.GameSession.ResolvePlacement"/>).
/// </summary>
public sealed record ResolvePlacementCommand(Cell? Target = null, Cell? Second = null) : GameCommand;

/// <summary>
/// Сыграть карточку из личного штаба одним шагом: проверка, бросок кубика и действие
/// (<see cref="Game.GameSession.PlayCard"/>).
/// </summary>
/// <param name="Cell">Ячейка карточки.</param>
/// <param name="Option">Индекс варианта действия карточки.</param>
/// <param name="Targets">Выбранные цели.</param>
public sealed record UseCardCommand(Cell Cell, int Option, CommandTargets Targets) : GameCommand;

/// <summary>
/// Применить свою карточку теории (<see cref="Game.GameSession.UseTheoryCard"/>).
/// </summary>
/// <param name="CardId">Идентификатор карточки теории.</param>
/// <param name="Targets">Выбранные цели.</param>
public sealed record UseTheoryCardCommand(int CardId, CommandTargets Targets) : GameCommand;

/// <summary>
/// Владелец основной теории выбирает карточку в руке для её действия (<see cref="Game.GameSession.SelectTriggerHandCard"/>).
/// </summary>
public sealed record SelectTriggerHandCardCommand(int CardId) : GameCommand;

/// <summary>
/// Владелец основной теории выбирает теорию для её действия (<see cref="Game.GameSession.SelectTriggerTheory"/>).
/// </summary>
/// <param name="Theory">Название теории.</param>
public sealed record SelectTriggerTheoryCommand(string Theory) : GameCommand;

/// <summary>
/// Владелец основной теории выбирает карточку на поле для её действия (<see cref="Game.GameSession.SelectTriggerCell"/>).
/// </summary>
public sealed record SelectTriggerCellCommand(Cell Cell) : GameCommand;

/// <summary>
/// Владелец основной теории отказывается от её действия (<see cref="Game.GameSession.SkipTheoryTrigger"/>).
/// </summary>
public sealed record SkipTheoryTriggerCommand : GameCommand;

/// <summary>
/// Спасовать до конца раунда (<see cref="Game.GameSession.Pass"/>).
/// </summary>
public sealed record PassCommand : GameCommand;

/// <summary>
/// Цели действия карточки — то, что игрок выбрал в <see cref="Game.GameContext"/>, в виде идентификаторов.
/// Не выбранные цели — <c>null</c>.
/// </summary>
public sealed record CommandTargets
{
  /// <summary>
  /// Теории по названиям, когда действию нужно несколько (<see cref="Effects.Effect.TheoryCount"/>), в порядке выбора.
  /// </summary>
  public IReadOnlyList<string>? Theories { get; init; }

  /// <summary>
  /// Теория по названию (<see cref="Game.GameContext.TargetTheory"/>).
  /// </summary>
  public string? Theory { get; init; }

  /// <summary>
  /// Теория-источник по названию (<see cref="Game.GameContext.SourceTheory"/>).
  /// </summary>
  public string? SourceTheory { get; init; }

  /// <summary>
  /// Идентификатор карточки в руке (<see cref="Game.GameContext.HandCard"/>).
  /// </summary>
  public int? HandCard { get; init; }

  /// <summary>
  /// Карточка на поле (<see cref="Game.GameContext.TargetCell"/>).
  /// </summary>
  public Cell? TargetCell { get; init; }

  /// <summary>
  /// Вторая карточка на поле (<see cref="Game.GameContext.SecondTargetCell"/>).
  /// </summary>
  public Cell? SecondTargetCell { get; init; }

  /// <summary>
  /// Карточка в личном штабе, на которую приводят адептов (<see cref="Game.GameContext.TargetAdeptCard"/>).
  /// </summary>
  public Cell? AdeptCard { get; init; }

  /// <summary>
  /// Место другого игрока за столом (<see cref="Game.GameContext.TargetPlayer"/>).
  /// </summary>
  public int? TargetPlayer { get; init; }

  /// <summary>
  /// Карточка, действие которой выполняет эта (<see cref="Game.GameContext.BorrowedCell"/>).
  /// </summary>
  public Cell? BorrowedCell { get; init; }

  /// <summary>
  /// Индекс варианта действия карточки <see cref="BorrowedCell"/> (<see cref="Game.GameContext.BorrowedOption"/>).
  /// </summary>
  public int? BorrowedOption { get; init; }

  /// <summary>
  /// Карточка, действие которой копируют (<see cref="Game.GameContext.CopiedCell"/>).
  /// </summary>
  public Cell? CopiedCell { get; init; }

  /// <summary>
  /// Индекс варианта действия карточки <see cref="CopiedCell"/> (<see cref="Game.GameContext.CopiedOption"/>).
  /// </summary>
  public int? CopiedOption { get; init; }

  /// <summary>
  /// Без целей.
  /// </summary>
  public static CommandTargets None { get; } = new();
}
