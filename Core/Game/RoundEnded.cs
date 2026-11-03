namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Конец раунда: штабы теорий проверены и закрыты. На это событие реагируют пассивные способности карточек теорий.
/// </summary>
public sealed record RoundEnded : GameEvent;
