using System.Security.Cryptography;
using ConspiracyLeague.Core.Cards;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Постоянные идентификаторы карточек партии: по ним команды по сети ссылаются на карточку
/// (в руке, на поле, карточку теории), ведь саму карточку по сети не передать.
/// </summary>
/// <remarks>
/// Идентификатор выдаётся карточке при первом обращении и не меняется, пока жива сама карточка
/// (новая карточка — например, после обучения или закрытая карта — получает новый идентификатор).
/// Идентификаторы случайные, а не порядковые: по ним нельзя узнать, когда и откуда пришла карточка,
/// сколько карточек взяли другие игроки или какая карточка лежит в колоде.
/// Выдаёт идентификаторы только сервер, клиенты получают их вместе с состоянием партии.
/// </remarks>
public sealed class CardRegistry
{
  private readonly Dictionary<Card, int> _ids = new(ReferenceEqualityComparer.Instance);
  private readonly Dictionary<int, Card> _cards = [];

  /// <summary>
  /// Идентификатор карточки; если его ещё нет — выдать новый.
  /// </summary>
  public int IdOf(Card card)
  {
    ArgumentNullException.ThrowIfNull(card);
    if (_ids.TryGetValue(card, out var id))
      return id;

    do
      id = RandomNumberGenerator.GetInt32(1, int.MaxValue);
    while (_cards.ContainsKey(id));

    _ids.Add(card, id);
    _cards.Add(id, card);
    return id;
  }

  /// <summary>
  /// Клиент: забыть все идентификаторы — сервер прислал новое состояние партии (<see cref="Client.ClientMirror"/>).
  /// </summary>
  internal void Clear()
  {
    _ids.Clear();
    _cards.Clear();
  }

  /// <summary>
  /// Клиент: запомнить идентификатор, который карточке выдал сервер.
  /// </summary>
  /// <exception cref="ArgumentException">Идентификатор неверный или уже занят.</exception>
  internal void Assign(Card card, int id)
  {
    ArgumentNullException.ThrowIfNull(card);
    if (id <= 0)
      throw new ArgumentException("Идентификатор карточки должен быть положительным.", nameof(id));
    if (_cards.ContainsKey(id) || _ids.ContainsKey(card))
      throw new ArgumentException($"Идентификатор {id} уже выдан.", nameof(id));

    _ids.Add(card, id);
    _cards.Add(id, card);
  }

  /// <summary>
  /// Карточка с идентификатором или <c>null</c>, если такой идентификатор не выдавался.
  /// </summary>
  public Card? Find(int id) => _cards.GetValueOrDefault(id);
}
