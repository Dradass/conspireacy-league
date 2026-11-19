using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Карточка на поле, восстановленная клиентом по виду партии (<see cref="PlayingField.Restore"/>).
/// </summary>
/// <param name="Cell">Ячейка.</param>
/// <param name="Card">Карточка.</param>
/// <param name="Used">Сыграна.</param>
/// <param name="Rotated">Повёрнута после быстрого действия.</param>
/// <param name="Trained">Изменение вероятности успеха обучением и ослаблением, %.</param>
/// <param name="Adepts">Адепты на карточке в личном штабе.</param>
/// <param name="Chip">Кто чипировал персонажа.</param>
/// <param name="Wheel">Чей значок руля на карточке.</param>
internal sealed record RestoredCell(Cell Cell, Card Card, bool Used, bool Rotated, int Trained, int Adepts, Player? Chip, Player? Wheel);
