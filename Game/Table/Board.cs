using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Рабочая поверхность: сетка ячеек с зонами игроков, общей зоной и рынком.
/// Правила выкладывания — в <see cref="PlayingField"/>, стол их только отображает.
/// </summary>
/// <remarks>
/// Стол должен стоять в начале координат: локальные координаты стола совпадают с мировыми.
/// </remarks>
public partial class Board : Node2D
{
  /// <summary>
  /// Размер ячейки: карточка плюс поля вокруг неё.
  /// </summary>
  public static readonly Vector2 CellSize = CardPainter.CardSize + new Vector2(20, 20);

  private const float SlotInset = 4f;
  private const float ZoneBorderWidth = 6f;
  private const double SnapDuration = 0.12;
  private const float AvailableFillAlpha = 0.18f;
  private const float AvailableBorderAlpha = 0.8f;
  private const float AvailableBorderWidth = 3f;
  private const int TotalsFontSize = 14;
  private const int TotalsValueFontSize = 28;
  private const int TotalsOwnerFontSize = 20;
  private const float WaitingBorderDarkening = 0.45f;
  private const int MarketDeckLayers = 3;
  private const float MarketDeckLayerOffset = 3f;
  private const double MarketOpenDuration = 0.35;
  private const double MarketOpenStagger = 0.12;
  private const double MarketShiftDuration = 0.4;
  private const double MarketDiscardDuration = 0.35;
  private const float MarketDiscardRise = 60f;
  private const int IncomeFontSize = 34;
  private const int IncomeTitleFontSize = 26;
  private const float IncomeRise = 70f;
  private const double IncomeStagger = 0.25;
  private const double IncomeFadeIn = 0.15;
  private const double IncomeHold = 1.1;
  private const double IncomeFadeOut = 0.35;

  private static readonly Color TableColor = new("26342c");
  private static readonly Color SlotColor = new("2c3d33");
  private static readonly Color CommonZoneColor = new("2f4a3a");
  private static readonly Color CommonSlotColor = new("37574a");
  private static readonly Color CommonBorderColor = new("6f9c80");
  private static readonly Color MoneyColor = new("e0b84f");
  private static readonly Color AllowedColor = new(0.55f, 0.95f, 0.6f, 0.35f);
  private static readonly Color PaidColor = new(1f, 0.85f, 0.3f, 0.4f);
  private static readonly Color DeniedColor = new(1f, 0.35f, 0.3f, 0.35f);
  private static readonly Color TotalsColor = new("d8e6dc");
  private static readonly Color ClosedColor = new("f2c230");
  private static readonly Color ClosedAdeptsTint = new(0.6f, 0.6f, 0.6f);
  private static readonly Color MarketZoneColor = new("3d3a2c");
  private static readonly Color MarketSlotColor = new("4a4634");
  private static readonly Color MarketBorderColor = new("b59a5a");
  private static readonly Color MarketDeckColor = new("5a4a7a");

  private readonly Dictionary<Vector2I, PlacementCheck> _available = new();
  private readonly Dictionary<Vector2I, CardView> _views = new();
  private readonly Dictionary<TheoryColumn, AdeptsView> _adepts = new();
  private readonly Dictionary<Vector2I, CardView> _marketViews = new();
  private int _marketCardsInFlight;
  private ColorRect _highlight = null!;
  private Label _highlightCost = null!;
  private Node2D _cardsLayer = null!;
  private Node2D _adeptsLayer = null!;
  private Node2D _effectsLayer = null!;

  /// <summary>
  /// Сессия игры: поле, которое отображает стол, и игроки. Зона каждого игрока — его цвета,
  /// у ходящего игрока рамка ярче.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Поле, которое отображает стол.
  /// </summary>
  public PlayingField Field => Session.Field;

  /// <summary>
  /// Рынок, который отображает стол.
  /// </summary>
  public Market Market => Session.Market;

  /// <summary>
  /// Размер стола в пикселях.
  /// </summary>
  public Vector2 Size => new(Field.Columns * CellSize.X, Field.Rows * CellSize.Y);

  public override void _Ready()
  {
    _highlight = new ColorRect
    {
      Name = "Highlight",
      Size = CellSize,
      Visible = false,
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    _highlightCost = new Label
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Bottom,
      Size = CellSize,
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    _highlightCost.AddThemeFontSizeOverride("font_size", 18);
    _highlightCost.AddThemeColorOverride("font_color", MoneyColor);
    _highlightCost.AddThemeColorOverride("font_outline_color", Colors.Black);
    _highlightCost.AddThemeConstantOverride("outline_size", 6);
    _highlight.AddChild(_highlightCost);
    AddChild(_highlight);

    _cardsLayer = new Node2D { Name = "Cards" };
    AddChild(_cardsLayer);
    _adeptsLayer = new Node2D { Name = "Adepts" };
    AddChild(_adeptsLayer);
    _effectsLayer = new Node2D { Name = "Effects" };
    AddChild(_effectsLayer);

    Field.AdeptsAdded += OnAdeptsAdded;
    Field.TheoryClosed += OnTheoryClosed;
    Field.UsedCardsRefreshed += OnUsedCardsRefreshed;
    Market.CardOpened += OnMarketCardOpened;
    Market.CardTaken += OnMarketCardTaken;
    Market.CardDiscarded += OnMarketCardDiscarded;
    Market.CardMoved += OnMarketCardMoved;
    Session.Turns.TurnChanged += OnTurnChanged;
  }

  public override void _ExitTree()
  {
    Field.AdeptsAdded -= OnAdeptsAdded;
    Field.TheoryClosed -= OnTheoryClosed;
    Field.UsedCardsRefreshed -= OnUsedCardsRefreshed;
    Market.CardOpened -= OnMarketCardOpened;
    Market.CardTaken -= OnMarketCardTaken;
    Market.CardDiscarded -= OnMarketCardDiscarded;
    Market.CardMoved -= OnMarketCardMoved;
    Session.Turns.TurnChanged -= OnTurnChanged;
  }

  public override void _Draw()
  {
    DrawRect(new Rect2(Vector2.Zero, Size), TableColor);
    DrawZone(Field.CommonZone, CommonZoneColor, CommonBorderColor);
    DrawZone(Market.Zone, MarketZoneColor, MarketBorderColor);
    foreach (var (player, zone) in Field.PlayerZones)
    {
      var border = PlayerPalette.MainOf(player.Color);
      if (player != Session.CurrentPlayer)
        border = border.Darkened(WaitingBorderDarkening);
      DrawZone(zone, PlayerPalette.ZoneOf(player.Color), border);
    }

    var slotSize = CellSize - new Vector2(SlotInset, SlotInset) * 2;
    for (var x = 0; x < Field.Columns; x++)
    {
      for (var y = 0; y < Field.Rows; y++)
      {
        var cell = new Vector2I(x, y);
        DrawRect(new Rect2(CellOrigin(cell) + new Vector2(SlotInset, SlotInset), slotSize), SlotColorOf(cell.ToCell()));
      }
    }

    DrawTheoryTotals();
    DrawMarketDeck();

    foreach (var (cell, check) in _available)
    {
      var slot = new Rect2(CellOrigin(cell) + new Vector2(SlotInset, SlotInset), slotSize);
      var color = check.Cost > 0 ? PaidColor : AllowedColor;
      DrawRect(slot, color with { A = AvailableFillAlpha });
      DrawRect(slot.Grow(-AvailableBorderWidth / 2), color with { A = AvailableBorderAlpha }, filled: false, width: AvailableBorderWidth);
    }
  }

  /// <summary>
  /// Подсветить все ячейки, куда можно положить взятую карточку.
  /// </summary>
  public void ShowAvailable(IEnumerable<(Cell Cell, PlacementCheck Check)> cells)
  {
    _available.Clear();
    foreach (var (cell, check) in cells)
      _available[new Vector2I(cell.Column, cell.Row)] = check;
    QueueRedraw();
  }

  /// <summary>
  /// Подсветить ячейки, которые можно выбрать целью действия.
  /// </summary>
  public void ShowTargets(IEnumerable<Cell> cells) => ShowAvailable(cells.Select(cell => (cell, PlacementCheck.Allowed)));

  /// <summary>
  /// Убрать подсветку доступных ячеек.
  /// </summary>
  public void HideAvailable()
  {
    _available.Clear();
    QueueRedraw();
  }

  /// <summary>
  /// Ячейка под точкой стола.
  /// </summary>
  /// <param name="point">Точка в мировых координатах.</param>
  /// <returns>Ячейка или <c>null</c>, если точка вне сетки.</returns>
  public Vector2I? CellAt(Vector2 point)
  {
    var local = ToLocal(point);
    var cell = new Vector2I(Mathf.FloorToInt(local.X / CellSize.X), Mathf.FloorToInt(local.Y / CellSize.Y));
    return cell.X >= 0 && cell.X < Field.Columns && cell.Y >= 0 && cell.Y < Field.Rows ? cell : null;
  }

  /// <summary>
  /// Карточка, лежащая в ячейке.
  /// </summary>
  public CardView? ViewAt(Vector2I cell) => _views.GetValueOrDefault(cell);

  /// <summary>
  /// Открытая карточка рынка, лежащая в ячейке.
  /// </summary>
  public CardView? MarketViewAt(Vector2I cell) => _marketViews.GetValueOrDefault(cell);

  /// <summary>
  /// Снять открытую карточку рынка со стола, чтобы её перетаскивать. Карточка остаётся на рынке,
  /// пока её не купят.
  /// </summary>
  public CardView TakeMarketView(Vector2I cell)
  {
    var view = _marketViews[cell];
    _marketViews.Remove(cell);
    _cardsLayer.RemoveChild(view);
    return view;
  }

  /// <summary>
  /// Вернуть карточку рынка в её ячейку. Карточка плавно доезжает до центра ячейки.
  /// </summary>
  public void ReturnMarketView(CardView view, Vector2I cell)
  {
    _marketViews[cell] = view;
    SnapTo(view, cell);
  }

  /// <summary>
  /// Центр ячейки.
  /// </summary>
  public static Vector2 CellCenter(Vector2I cell) => CellOrigin(cell) + CellSize / 2;

  /// <summary>
  /// Центр зоны.
  /// </summary>
  public static Vector2 ZoneCenter(Zone zone) => ZoneRect(zone).GetCenter();

  /// <summary>
  /// Положить карточку в ячейку. Карточка плавно доезжает до центра ячейки.
  /// </summary>
  /// <remarks>
  /// Проверять правила и класть карточку в <see cref="Field"/> нужно до вызова.
  /// </remarks>
  public void Place(CardView view, Vector2I cell)
  {
    _views[cell] = view;
    // Перерисовываем итоги теорий под общей зоной.
    QueueRedraw();
    SnapTo(view, cell);
  }

  private void SnapTo(CardView view, Vector2I cell)
  {
    view.Lifted = false;
    _cardsLayer.AddChild(view);
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(cell), SnapDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
  }

  /// <summary>
  /// Подсветить ячейку, куда будет положена карточка.
  /// </summary>
  /// <param name="cell">Ячейка или <c>null</c>, чтобы убрать подсветку.</param>
  /// <param name="check">Можно ли положить туда карточку.</param>
  public void Highlight(Vector2I? cell, PlacementCheck? check = null)
  {
    _highlight.Visible = cell is not null;
    if (cell is not { } target)
      return;

    var cost = check is { IsAllowed: true } ? check.Cost : 0;
    _highlight.Position = CellOrigin(target);
    _highlight.Color = check is not { IsAllowed: true } ? DeniedColor : cost > 0 ? PaidColor : AllowedColor;
    _highlightCost.Text = cost > 0 ? $"−{cost}$" : string.Empty;
  }

  /// <summary>
  /// Показать доход теории от адептов: над каждой карточкой стопки всплывает, сколько получил её владелец
  /// (цветом владельца), над стопкой — сколько адептов принесли деньги.
  /// </summary>
  /// <returns>Задача, которая завершается, когда анимация закончилась.</returns>
  public async Task ShowIncome(TheoryIncome income)
  {
    var column = income.Column;
    var title = $"{column.Theory}: {column.Adepts} адептов → {income.Total}$";
    SpawnFloatingText(title, TotalsColor, IncomeTitleFontSize, CellCenter(column.Top.Above.ToVector2I()), 0);

    var delay = IncomeStagger;
    foreach (var member in income.Members.Where(member => member.Amount > 0))
    {
      var color = PlayerPalette.MainOf(member.Member.Owner.Color);
      SpawnFloatingText($"+{member.Amount}$", color, IncomeFontSize, CellCenter(member.Member.Cell.ToVector2I()), delay);
      delay += IncomeStagger;
    }

    var duration = delay + IncomeFadeIn + IncomeHold + IncomeFadeOut;
    await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
  }

  /// <summary>
  /// Надпись над столом: появляется, поднимается вверх и тает.
  /// </summary>
  private void SpawnFloatingText(string text, Color color, int fontSize, Vector2 center, double delay)
  {
    var label = new Label
    {
      Text = text,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      Modulate = Colors.Transparent,
    };
    label.AddThemeFontSizeOverride("font_size", fontSize);
    label.AddThemeColorOverride("font_color", color);
    label.AddThemeColorOverride("font_outline_color", Colors.Black);
    label.AddThemeConstantOverride("outline_size", 8);
    _effectsLayer.AddChild(label);
    label.Size = label.GetMinimumSize();
    label.Position = center - label.Size / 2;

    var tween = label.CreateTween();
    tween.TweenInterval(delay);
    tween.TweenProperty(label, CanvasItem.PropertyName.Modulate.ToString(), Colors.White, IncomeFadeIn);
    tween.Parallel()
      .TweenProperty(label, Control.PropertyName.Position.ToString(), label.Position + Vector2.Up * IncomeRise, IncomeFadeIn + IncomeHold)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
    tween.TweenProperty(label, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, IncomeFadeOut);
    tween.TweenCallback(Callable.From(label.QueueFree));
  }

  private void OnTurnChanged(Player player) => QueueRedraw();

  /// <summary>
  /// Штаб теории закрыт: все его карточки сереют, на первой — лента «CLOSED».
  /// </summary>
  private void OnTheoryClosed(TheoryColumn column)
  {
    foreach (var cell in column.Cells)
    {
      if (_views.TryGetValue(cell.ToVector2I(), out var view))
        view.MarkClosed(withRibbon: cell == column.TheoryCell);
    }

    if (_adepts.TryGetValue(column, out var adepts))
      adepts.Modulate = ClosedAdeptsTint;
    // Перерисовываем итоги теорий: под закрытой теорией это подписано.
    QueueRedraw();
  }

  /// <summary>
  /// Сыгранные карточки личных штабов переворачиваются лицевой стороной вверх.
  /// </summary>
  private void OnUsedCardsRefreshed(IReadOnlyList<Cell> cells)
  {
    foreach (var cell in cells)
      ViewAt(cell.ToVector2I())?.FlipFaceUp();
  }

  /// <summary>
  /// Карточка ушла с рынка при обновлении: она поднимается и тает.
  /// </summary>
  private void OnMarketCardDiscarded(Cell cell, PersonCard card)
  {
    if (!_marketViews.Remove(cell.ToVector2I(), out var view))
      return;

    var tween = view.CreateTween();
    tween.TweenProperty(view, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, MarketDiscardDuration);
    tween.Parallel().TweenProperty(view, Node2D.PropertyName.Position.ToString(), view.Position + Vector2.Up * MarketDiscardRise, MarketDiscardDuration);
    tween.TweenCallback(Callable.From(view.QueueFree));
  }

  /// <summary>
  /// Карточка рынка сдвинулась при обновлении: она плавно переезжает в новую ячейку.
  /// </summary>
  private void OnMarketCardMoved(Cell from, Cell to, PersonCard card)
  {
    if (!_marketViews.Remove(from.ToVector2I(), out var view))
      return;

    _marketViews[to.ToVector2I()] = view;
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(to.ToVector2I()), MarketShiftDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.InOut);
  }

  /// <summary>
  /// Карточка открыта с рынка: она выезжает из колоды рынка в свою ячейку. Если карточки открываются
  /// одна за другой, они выезжают по очереди.
  /// </summary>
  private void OnMarketCardOpened(Cell cell, PersonCard card)
  {
    var target = cell.ToVector2I();
    var view = CardView.Create(card);
    view.Position = CellCenter(Market.DeckCell.ToVector2I());
    _cardsLayer.AddChild(view);
    _marketViews[target] = view;
    // Перерисовываем колоду рынка: в ней стало меньше карточек.
    QueueRedraw();

    var tween = view.CreateTween();
    tween.TweenInterval(MarketOpenStagger * _marketCardsInFlight++);
    tween.TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(target), MarketOpenDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
    tween.TweenCallback(Callable.From(() => _marketCardsInFlight--));
  }

  private void OnAdeptsAdded(TheoryColumn column, int count)
  {
    if (!_adepts.TryGetValue(column, out var view))
    {
      view = new AdeptsView { Name = $"Adepts{column.Theory.Name}", Position = CellCenter(column.TheoryCell.ToVector2I()) };
      _adeptsLayer.AddChild(view);
      _adepts[column] = view;
    }

    view.Add(count);
    // Перерисовываем итоги теорий: под теорией подписано количество адептов.
    QueueRedraw();
  }

  /// <summary>
  /// Под каждым столбцом теории, в ряду под общей зоной, — суммарное влияние, количество человек и адептов,
  /// а под штабом теории — ещё и его владелец с его влиянием.
  /// </summary>
  private void DrawTheoryTotals()
  {
    var row = Field.CommonZone.BottomRow + 1;
    if (row >= Field.Rows)
      return;

    // В одном столбце могут оказаться две теории (дочерняя над чужой родительской) — тогда подписываем названия.
    foreach (var group in Field.TheoryColumns.GroupBy(column => column.Cells[0].Column))
    {
      var area = new Rect2(CellOrigin(new Vector2I(group.Key, row)), CellSize).Grow(-SlotInset);
      if (group.Count() == 1)
      {
        DrawTotals(group.Single(), area);
        continue;
      }

      var text = string.Join("\n", group.Select(column =>
        $"{column.Theory}:\nвлияние {column.Influence}, человек {column.Cards.Count}, адептов {column.Adepts}"
        + (column.IsHeadquarters ? $", владелец {column.Owner}" : string.Empty)));
      CardPainter.DrawCenteredText(this, text, area, TotalsFontSize, TotalsColor);
    }
  }

  private void DrawTotals(TheoryColumn column, Rect2 area)
  {
    List<(string Text, int FontSize, Color Color)> lines =
    [
      ("Влияние", TotalsFontSize, TotalsColor),
      (column.Influence.ToString(), TotalsValueFontSize, TotalsColor),
      ("Человек", TotalsFontSize, TotalsColor),
      (column.Cards.Count.ToString(), TotalsValueFontSize, TotalsColor),
      ("Адептов", TotalsFontSize, TotalsColor),
      (column.Adepts.ToString(), TotalsValueFontSize, TotalsColor),
    ];
    if (column.IsClosed)
      lines.Add(("Закрыт", TotalsFontSize, ClosedColor));
    if (column.IsHeadquarters)
    {
      var owner = column.Owner;
      lines.Add(("Владелец", TotalsFontSize, TotalsColor));
      lines.Add(($"{owner.Name} ({column.InfluenceOf(owner)})", TotalsOwnerFontSize, PlayerPalette.MainOf(owner.Color)));
    }
    // Высота строки пропорциональна размеру шрифта.
    var scale = area.Size.Y / lines.Sum(line => line.FontSize);
    var top = area.Position.Y;
    foreach (var (text, fontSize, color) in lines)
    {
      var lineArea = new Rect2(area.Position.X, top, area.Size.X, fontSize * scale);
      CardPainter.DrawCenteredText(this, text, lineArea, fontSize, color);
      top = lineArea.End.Y;
    }
  }

  /// <summary>
  /// Карточку купили с рынка: убираем её со стола, если она ещё там (её не унесли перетаскиванием).
  /// </summary>
  private void OnMarketCardTaken(Cell cell, PersonCard card)
  {
    if (_marketViews.Remove(cell.ToVector2I(), out var view))
      view.QueueFree();
  }

  /// <summary>
  /// Колода рынка: стопка карточек рубашкой вверх с количеством оставшихся карточек.
  /// </summary>
  private void DrawMarketDeck()
  {
    var center = CellCenter(Market.DeckCell.ToVector2I());
    var layers = Mathf.Min(Market.DeckCount, MarketDeckLayers);
    for (var i = 0; i < layers; i++)
    {
      var offset = new Vector2(MarketDeckLayerOffset, -MarketDeckLayerOffset) * (i - (layers - 1) / 2f);
      var title = i == layers - 1 ? $"Рынок\n{Market.DeckCount}" : null;
      CardPainter.DrawBack(this, new Rect2(center - CardPainter.CardSize / 2 + offset, CardPainter.CardSize), MarketDeckColor, title);
    }
  }

  private void DrawZone(Zone zone, Color fill, Color border)
  {
    var rect = ZoneRect(zone);
    DrawRect(rect, fill);
    DrawRect(rect.Grow(-ZoneBorderWidth / 2), border, filled: false, width: ZoneBorderWidth);
  }

  private Color SlotColorOf(Cell cell)
  {
    if (Field.CommonZone.Contains(cell))
      return CommonSlotColor;
    if (Market.Contains(cell))
      return MarketSlotColor;

    foreach (var (player, zone) in Field.PlayerZones)
    {
      if (zone.Contains(cell))
        return PlayerPalette.SlotOf(player.Color);
    }

    return SlotColor;
  }

  private static Rect2 ZoneRect(Zone zone) =>
    new(CellOrigin(new Vector2I(zone.Left, zone.Top)), new Vector2(zone.Width * CellSize.X, zone.Height * CellSize.Y));

  private static Vector2 CellOrigin(Vector2I cell) => new(cell.X * CellSize.X, cell.Y * CellSize.Y);
}

/// <summary>
/// Перевод ячеек между Godot и Core.
/// </summary>
public static class CellExtensions
{
  /// <summary>
  /// Ячейка поля из ячейки стола.
  /// </summary>
  public static Cell ToCell(this Vector2I cell) => new(cell.X, cell.Y);

  /// <summary>
  /// Ячейка стола из ячейки поля.
  /// </summary>
  public static Vector2I ToVector2I(this Cell cell) => new(cell.Column, cell.Row);
}
