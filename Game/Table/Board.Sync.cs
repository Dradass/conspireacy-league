using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Сетевая игра: партию пересобирает клиент по виду от сервера (<see cref="Core.Client.ClientMirror"/>), и событий
/// поля, по которым стол обычно меняется по шагам, нет — после каждого обновления (<see cref="GameSession.Restored"/>)
/// стол приводится к партии целиком. Виды карточек по возможности остаются прежними: карточка, которая
/// переехала, плавно едет на новое место, новая — появляется, пропавшая — тает.
/// </summary>
public partial class Board
{
  private const double SyncFadeDuration = 0.3;

  /// <summary>
  /// Сколько адептов показано на теории (по названию) — чтобы добавить или убрать только разницу.
  /// </summary>
  private readonly Dictionary<string, int> _shownAdepts = [];

  private Zone? _syncedCommonZone;

  /// <summary>
  /// Игрок этого компьютера в сетевой игре: он видит свои закрытые карточки и те, в которые смотрел. В игре
  /// за одним компьютером — <c>null</c>, и смотрит тот, кто сейчас ходит.
  /// </summary>
  public Player? LocalPlayer { get; init; }

  /// <summary>
  /// Чьими глазами смотрит стол: игрок этого компьютера или, в игре за одним компьютером, тот, кто ходит.
  /// </summary>
  public Player Viewer => LocalPlayer ?? Session.CurrentPlayer;

  /// <summary>
  /// Чья рука показана на экране (<see cref="Ui.HandHub.Owner"/>): для него рисуются подсказки теорий над карточками.
  /// </summary>
  public Func<Player>? HandOwner { get; set; }

  /// <summary>
  /// Для кого подсказки теорий (<see cref="CardTheoryHints"/>): игрок этого компьютера или игрок, чья рука на экране —
  /// пока ходит бот, это последний ходивший человек, а не бот.
  /// </summary>
  public Player HintsViewer => LocalPlayer ?? HandOwner?.Invoke() ?? Session.CurrentPlayer;

  /// <summary>
  /// Привести стол к партии целиком (см. описание класса).
  /// </summary>
  private void OnRestored()
  {
    SyncFieldCards();
    SyncMarket();
    SyncTheoryCards();
    SyncAdepts();
    SyncCardAdepts();
    SyncLeaderMeeples();
    _firstPlayerToken.MoveTo(FirstPlayerTokenPosition(Session.Turns.FirstPlayer), animate: true);
    if (Radial)
    {
      TurnPlayersTo(Viewer);
      Relayout(animate: true);
    }
    else if (_syncedCommonZone != Field.CommonZone)
      Relayout(animate: true);
    _syncedCommonZone = Field.CommonZone;

    UpdateStackOrder();
    UpdateSuccessModifiers();
    QueueRedraw();
  }

  private void SyncFieldCards()
  {
    var old = new Dictionary<Card, (Vector2I Cell, CardView View)>(ReferenceEqualityComparer.Instance);
    foreach (var (cell, view) in _views)
      old.TryAdd(view.Card, (cell, view));
    _views.Clear();

    foreach (var (player, zone) in Field.PlayerZones)
    {
      foreach (var cell in zone.Cells)
      {
        if (Field.CardAt(cell) is not { } card)
          continue;

        var view = SyncView(old, card, cell.ToVector2I());
        view.CardOwner = null;
        view.Chipped = Field.Chips.ContainsKey(cell);
        view.Wheeled = Field.Wheels.ContainsKey(cell);
        view.Rotated = Field.IsRotated(cell);
        if (Field.IsUsed(cell))
          view.FlipFaceDown();
        else
          view.FlipFaceUp();
      }
    }

    foreach (var column in Field.TheoryColumns)
    {
      foreach (var member in column.Members)
      {
        var view = SyncView(old, member.Card, member.Cell.ToVector2I());
        view.CardOwner = member.Owner;
        view.IsHeadquarters = column.IsHeadquarters && member.Cell == column.TheoryCell;
        if (column.IsClosed)
          view.MarkClosed(withRibbon: member.Cell == column.TheoryCell);
        else
          view.ClearClosed();
        view.QueueRedraw();
      }
    }

    foreach (var (_, (_, view)) in old)
      FadeOut(view);
  }

  /// <summary>
  /// Вид карточки в ячейке: прежний (переезжает, если лежал в другой ячейке) или новый.
  /// </summary>
  private CardView SyncView(Dictionary<Card, (Vector2I Cell, CardView View)> old, Card card, Vector2I cell)
  {
    CardView view;
    if (old.Remove(card, out var previous) && IsInstanceValid(previous.View))
    {
      view = previous.View;
      if (previous.Cell != cell)
        SlideTo(view, cell);
    }
    else
    {
      view = CardView.Create(card);
      view.Position = CardCenter(cell);
      view.BaseAngle = CellAngle(cell);
      _cardsLayer.AddChild(view);
    }

    _views[cell] = view;
    return view;
  }

  private void SyncMarket()
  {
    var old = new Dictionary<Card, (Vector2I Cell, CardView View)>(ReferenceEqualityComparer.Instance);
    foreach (var (cell, view) in _marketViews)
      old.TryAdd(view.Card, (cell, view));
    _marketViews.Clear();

    var opened = 0;
    foreach (var (cell, card) in Market.OpenCards)
    {
      var key = cell.ToVector2I();
      if (old.Remove(card, out var previous) && IsInstanceValid(previous.View))
      {
        _marketViews[key] = previous.View;
        if (previous.Cell != key)
          MoveCard(previous.View, CellCenter(key), CellAngle(key), MarketShiftDuration);
        continue;
      }

      // Новая карточка рынка выезжает из колоды.
      var view = CardView.Create(card);
      view.Position = CellCenter(Market.DeckCell.ToVector2I());
      view.BaseAngle = CellAngle(key);
      _cardsLayer.AddChild(view);
      _marketViews[key] = view;
      var tween = view.CreateTween();
      tween.TweenInterval(MarketOpenStagger * opened++);
      tween.TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(key), MarketOpenDuration)
        .SetTrans(Tween.TransitionType.Cubic)
        .SetEase(Tween.EaseType.Out);
    }

    foreach (var (_, (_, view)) in old)
      FadeOut(view);
  }

  private void SyncTheoryCards()
  {
    var old = new Dictionary<Card, CardView>(ReferenceEqualityComparer.Instance);
    foreach (var (owned, view) in _theoryViews)
      old.TryAdd(owned.Card, view);
    _theoryViews.Clear();

    foreach (var owned in Field.TheoryCards)
    {
      var cell = owned.Cell.ToVector2I();
      if (!old.Remove(owned.Card, out var view) || !IsInstanceValid(view))
      {
        view = CardView.Create(owned.Card);
        view.Position = CellCenter(cell);
        view.BaseAngle = CellAngle(cell);
        _cardsLayer.AddChild(view);
      }
      else
        MoveCard(view, CellCenter(cell), CellAngle(cell), SnapDuration);

      view.CardOwner = owned.Owner;
      _theoryViews[owned] = view;
      ShowTheoryCardFace(owned, view);
    }

    foreach (var (_, view) in old)
      FadeOut(view);
  }

  private void SyncAdepts()
  {
    var old = _adepts.ToDictionary(pair => pair.Key.Theory.Name, pair => pair.Value);
    _adepts.Clear();

    foreach (var column in Field.TheoryColumns)
    {
      var name = column.Theory.Name;
      var cell = column.TheoryCell.ToVector2I();
      if (old.Remove(name, out var view))
        MoveNode(view, CellCenter(cell), CellAngle(cell), SnapDuration);
      else if (column.Adepts > 0)
      {
        view = new AdeptsView { Name = $"Adepts{name}", Position = CellCenter(cell), Rotation = CellAngle(cell), Look = StyleOf(column).Look, Color = StyleOf(column).Color };
        _adeptsLayer.AddChild(view);
        _shownAdepts[name] = 0;
      }
      else
        continue;

      var shown = _shownAdepts.GetValueOrDefault(name);
      if (column.Adepts > shown)
        view.Add(column.Adepts - shown);
      else if (column.Adepts < shown)
        view.ShrinkTo(column.Adepts);
      _shownAdepts[name] = column.Adepts;
      view.Modulate = column.IsClosed ? ClosedAdeptsTint : Colors.White;
      _adepts[column] = view;
    }

    foreach (var (name, view) in old)
    {
      _shownAdepts.Remove(name);
      view.QueueFree();
    }
  }

  private void SyncCardAdepts()
  {
    foreach (var (cell, view) in _cardAdepts.ToList())
    {
      if (Field.AdeptsAt(cell.ToCell()) > 0)
        continue;
      _cardAdepts.Remove(cell);
      view.QueueFree();
    }

    foreach (var zone in Field.PlayerZones.Values)
    {
      foreach (var cell in zone.Cells)
      {
        var count = Field.AdeptsAt(cell);
        if (count == 0)
          continue;

        var key = cell.ToVector2I();
        if (!_cardAdepts.TryGetValue(key, out var view))
        {
          view = new AdeptsView { Name = $"Adepts{key.X}_{key.Y}", Position = CardCenter(key), Rotation = CellAngle(key) };
          _adeptsLayer.AddChild(view);
          _cardAdepts[key] = view;
          view.Add(count);
        }

        view.Total = count;
      }
    }
  }

  private void SyncLeaderMeeples()
  {
    var old = _leaderMeeples.ToDictionary(pair => pair.Key.Theory.Name, pair => pair.Value);
    _leaderMeeples.Clear();

    foreach (var column in Field.TheoryColumns)
    {
      var colors = column.LeaderMeeples.Select(player => PlayerPalette.MainOf(player.Color)).ToList();
      if (old.Remove(column.Theory.Name, out var meeples) && meeples.Select(meeple => meeple.Color).SequenceEqual(colors))
      {
        _leaderMeeples[column] = meeples;
        var angle = CellAngle(column.TheoryCell.ToVector2I());
        for (var i = 0; i < meeples.Count; i++)
          MoveNode(meeples[i], LeaderMeeplePosition(column, i, meeples.Count), angle, SnapDuration);
        continue;
      }

      if (meeples is not null)
        old[column.Theory.Name] = meeples;
      foreach (var player in column.LeaderMeeples)
        OnLeaderMeepleAdded(column, player);
    }

    foreach (var meeple in old.Values.SelectMany(meeples => meeples))
      FadeOut(meeple);
  }

  private static void FadeOut(Node2D node)
  {
    if (!IsInstanceValid(node))
      return;

    var tween = node.CreateTween();
    tween.TweenProperty(node, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, SyncFadeDuration);
    tween.TweenCallback(Callable.From(node.QueueFree));
  }
}
