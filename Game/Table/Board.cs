using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;
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
  private const double IncomeStagger = 0.5;
  private const double IncomeFadeIn = 0.3;
  private const double IncomeHold = 2.2;
  private const double IncomeFadeOut = 0.7;

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
  private static readonly Color ScientistBonusColor = new("4fd16a");
  private const int ScientistBonusFontSize = 20;
  private const double PeekShowDuration = 2.0;

  private const float LeaderMeepleScale = 0.85f;
  private const float LeaderMeepleDropHeight = 70f;
  private const double LeaderMeepleDropDuration = 0.45;
  private const double LeaderMeepleShiftDuration = 0.2;
  private const double LeaderMeepleFadeDuration = 0.4;
  private static readonly Color MarketZoneColor = new("3d3a2c");
  private static readonly Color MarketSlotColor = new("4a4634");
  private static readonly Color MarketBorderColor = new("b59a5a");
  private static readonly Color MarketDeckColor = new("5a4a7a");

  private readonly Dictionary<Vector2I, PlacementCheck> _available = new();

  /// <summary>
  /// Перегородка, в которую можно вставить теорию: номер столбца справа от неё, или <c>null</c>.
  /// </summary>
  private int? _wall;

  /// <summary>
  /// Половина ширины перегородки между столбцами, px: в таком расстоянии от границы столбцов считается, что карточка в перегородке.
  /// </summary>
  private const float WallHalfWidth = 10f;

  private static readonly Color WallColor = new(0.55f, 0.95f, 0.6f, 0.8f);
  private readonly Dictionary<Vector2I, CardView> _views = new();

  /// <summary>
  /// Все виды карточек на столе: ячейка, по которой они лежат в словаре, и вид. Для отладки (<see cref="DebugStateDumper"/>).
  /// </summary>
  public IEnumerable<(Vector2I Cell, CardView View)> AllViews => _views.Select(pair => (pair.Key, pair.Value));
  private readonly Dictionary<TheoryColumn, AdeptsView> _adepts = new();
  private readonly Dictionary<Vector2I, AdeptsView> _cardAdepts = new();
  private readonly Dictionary<TheoryColumn, List<MeepleView>> _leaderMeeples = new();
  private readonly Dictionary<Vector2I, CardView> _marketViews = new();
  private readonly Dictionary<OwnedTheoryCard, CardView> _theoryViews = new();
  private int _marketCardsInFlight;
  private readonly HashSet<CardView> _peeked = [];
  private readonly HashSet<CardView> _shownHidden = [];
  private ColorRect _highlight = null!;
  private Label _highlightCost = null!;
  private Node2D _cardsLayer = null!;
  private Node2D _adeptsLayer = null!;
  private Node2D _effectsLayer = null!;
  private FirstPlayerToken _firstPlayerToken = null!;

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
    _firstPlayerToken = new FirstPlayerToken { Name = "FirstPlayerToken" };
    AddChild(_firstPlayerToken);
    _firstPlayerToken.MoveTo(FirstPlayerTokenPosition(Session.Turns.FirstPlayer), animate: false);
    _effectsLayer = new Node2D { Name = "Effects" };
    AddChild(_effectsLayer);

    Field.AdeptsAdded += OnAdeptsAdded;
    Field.CardAdeptsAdded += OnCardAdeptsAdded;
    Field.TheoryClosed += OnTheoryClosed;
    Field.CommonZoneChanged += OnCommonZoneChanged;
    Field.UsedCardsRefreshed += OnUsedCardsRefreshed;
    Field.LeaderMeepleAdded += OnLeaderMeepleAdded;
    Field.CardEarned += OnCardEarned;
    Field.CardTrained += OnCardTrained;
    Field.AdeptsMoved += OnAdeptsMoved;
    Field.CardTakenToHand += OnCardTakenToHand;
    Field.LeaderMeeplesRemoved += OnLeaderMeeplesRemoved;
    Field.HiddenCardRevealed += OnHiddenCardRevealed;
    Field.HiddenCardPeeked += OnHiddenCardPeeked;
    Field.HiddenCardPlaced += OnHiddenCardPlaced;
    Field.TheoryEarned += OnTheoryEarned;
    Field.TheoryCardRemoved += OnTheoryCardRemoved;
    Field.CardReplaced += OnCardReplaced;
    Field.CardsSwapped += OnCardsSwapped;
    Field.TheoryColumnMoved += OnTheoryColumnMoved;
    Field.TheoryCardGranted += OnTheoryCardGranted;
    Field.TheoryCardTransferred += OnTheoryCardTransferred;
    Field.CardRemoved += OnCardTakenToHand;
    Field.CharacterChipped += OnCharacterChipped;
    Field.TheoryCardUsed += OnTheoryCardUsed;
    Field.TheoryCardsRefreshed += OnTheoryCardsRefreshed;
    Field.AdeptsRemoved += OnAdeptsRemoved;
    Market.CardOpened += OnMarketCardOpened;
    Market.CardTaken += OnMarketCardTaken;
    Market.CardDiscarded += OnMarketCardDiscarded;
    Market.CardMoved += OnMarketCardMoved;
    Session.Turns.TurnChanged += OnTurnChanged;
    Session.Turns.FirstPlayerChanged += OnFirstPlayerChanged;
  }

  public override void _ExitTree()
  {
    Field.AdeptsAdded -= OnAdeptsAdded;
    Field.CardAdeptsAdded -= OnCardAdeptsAdded;
    Field.TheoryClosed -= OnTheoryClosed;
    Field.CommonZoneChanged -= OnCommonZoneChanged;
    Field.UsedCardsRefreshed -= OnUsedCardsRefreshed;
    Field.LeaderMeepleAdded -= OnLeaderMeepleAdded;
    Field.CardEarned -= OnCardEarned;
    Field.CardTrained -= OnCardTrained;
    Field.AdeptsMoved -= OnAdeptsMoved;
    Field.CardTakenToHand -= OnCardTakenToHand;
    Field.LeaderMeeplesRemoved -= OnLeaderMeeplesRemoved;
    Field.HiddenCardRevealed -= OnHiddenCardRevealed;
    Field.HiddenCardPeeked -= OnHiddenCardPeeked;
    Field.HiddenCardPlaced -= OnHiddenCardPlaced;
    Field.TheoryEarned -= OnTheoryEarned;
    Field.TheoryCardRemoved -= OnTheoryCardRemoved;
    Field.CardReplaced -= OnCardReplaced;
    Field.CardsSwapped -= OnCardsSwapped;
    Field.TheoryColumnMoved -= OnTheoryColumnMoved;
    Field.TheoryCardGranted -= OnTheoryCardGranted;
    Field.TheoryCardTransferred -= OnTheoryCardTransferred;
    Field.CardRemoved -= OnCardTakenToHand;
    Field.CharacterChipped -= OnCharacterChipped;
    Field.TheoryCardUsed -= OnTheoryCardUsed;
    Field.TheoryCardsRefreshed -= OnTheoryCardsRefreshed;
    Field.AdeptsRemoved -= OnAdeptsRemoved;
    Market.CardOpened -= OnMarketCardOpened;
    Market.CardTaken -= OnMarketCardTaken;
    Market.CardDiscarded -= OnMarketCardDiscarded;
    Market.CardMoved -= OnMarketCardMoved;
    Session.Turns.TurnChanged -= OnTurnChanged;
    Session.Turns.FirstPlayerChanged -= OnFirstPlayerChanged;
  }

  public override void _Process(double delta)
  {
    UpdateShields();
    // Игрок, который сейчас ходит, видит закрытую карточку, пока держит над ней мышь, если это его карточка
    // или он в неё уже смотрел. Только что подсмотренная карточка какое-то время видна и без мыши.
    var hovered = CellAt(GetGlobalMousePosition()) is { } cell ? ViewAt(cell) : null;
    var player = Session.CurrentPlayer;
    var known = hovered is { Card: HiddenCard hidden } && (hovered.CardOwner == player || hidden.IsKnownBy(player));
    var peeked = new HashSet<CardView>(_shownHidden.Where(IsInstanceValid));
    if (known)
      peeked.Add(hovered!);
    // Свою карточку, лежащую рубашкой вверх (сыгранную в личном штабе или перевёрнутую карточку теории), игрок видит,
    // пока на неё наведён курсор.
    if (CellAt(GetGlobalMousePosition()) is { } hoverCell)
    {
      var hoveredCell = hoverCell.ToCell();
      if (Field.PlayerZones.TryGetValue(player, out var zone) && zone.Contains(hoveredCell) && Field.IsUsed(hoveredCell)
        && ViewAt(hoverCell) is { } playedView)
        peeked.Add(playedView);
      if (TheoryCardAt(hoverCell) is { Owner: var owner, IsUsed: true } theory && owner == player
        && _theoryViews.TryGetValue(theory, out var theoryView))
        peeked.Add(theoryView);
    }
    if (Field.CanSeeAllHidden(player))
      peeked.UnionWith(_views.Values.Where(view => view.Card is HiddenCard { IsRevealed: false }));
    if (peeked.SetEquals(_peeked))
      return;

    foreach (var view in _peeked.Except(peeked).Where(IsInstanceValid))
      view.Peeked = false;
    foreach (var view in peeked)
      view.Peeked = true;
    _peeked.Clear();
    _peeked.UnionWith(peeked);
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

    if (_wall is { } boundary)
    {
      var top = CellOrigin(new Vector2I(0, Field.CommonZone.BottomRow - 1)).Y;
      DrawRect(new Rect2(boundary * CellSize.X - WallHalfWidth, top, WallHalfWidth * 2, CellSize.Y), WallColor);
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
  /// Подсветить перегородку между столбцами <paramref name="boundary"/> - 1 и <paramref name="boundary"/>,
  /// или убрать подсветку, если <c>null</c>.
  /// </summary>
  public void ShowWall(int? boundary)
  {
    _wall = boundary;
    QueueRedraw();
  }

  /// <summary>
  /// Перегородка между столбцами в ряду дочерних теорий под точкой стола, или <c>null</c>, если точка не в перегородке.
  /// </summary>
  /// <returns>Номер столбца справа от перегородки.</returns>
  public int? WallAt(Vector2 point)
  {
    var local = ToLocal(point);
    var boundary = Mathf.RoundToInt(local.X / CellSize.X);
    var row = Mathf.FloorToInt(local.Y / CellSize.Y);
    var inWall = Mathf.Abs(local.X - boundary * CellSize.X) <= WallHalfWidth;
    return inWall && row == Field.CommonZone.BottomRow - 1 ? boundary : null;
  }

  /// <summary>
  /// Убрать подсветку доступных ячеек.
  /// </summary>
  public void HideAvailable()
  {
    _available.Clear();
    QueueRedraw();
  }

  /// <summary>
  /// Подсвечена ли ячейка как доступная.
  /// </summary>
  public bool IsAvailable(Vector2I cell) => _available.ContainsKey(cell);

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
  /// Карточка теории в ячейке ряда карточек теорий или <c>null</c>.
  /// </summary>
  public OwnedTheoryCard? TheoryCardAt(Vector2I cell) => Field.TheoryCards.FirstOrDefault(owned => owned.Cell == cell.ToCell());

  /// <summary>
  /// Вид карточки теории на столе.
  /// </summary>
  public CardView? TheoryViewOf(OwnedTheoryCard owned) => _theoryViews.GetValueOrDefault(owned);

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
    UpdateSuccessModifiers();
  }

  /// <summary>
  /// Показать на карточках, насколько изменена вероятность успеха персонажей
  /// (соседние бездомные и учёные в личном штабе).
  /// </summary>
  private void UpdateSuccessModifiers()
  {
    foreach (var (cell, view) in _views)
      view.SuccessModifier = Field.SuccessModifierAt(cell.ToCell());
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
  /// Показать награду владельцу теории в итоговом подсчёте: над стопкой — за скольких лидеров,
  /// над первой карточкой владельца — сколько он получил (цветом владельца).
  /// </summary>
  /// <returns>Задача, которая завершается, когда анимация закончилась.</returns>
  public async Task ShowLeadersBonus(LeadersBonus bonus)
  {
    var column = bonus.Column;
    var leaders = bonus.Amount / LeadersBonus.PerLeader;
    var title = $"{column.Theory}: {leaders} лидеров × {LeadersBonus.PerLeader}$ → {bonus.Owner.Name}";
    SpawnFloatingText(title, TotalsColor, IncomeTitleFontSize, CellCenter(column.Top.Above.ToVector2I()), 0);

    var cell = column.MembersOf(bonus.Owner).FirstOrDefault()?.Cell ?? column.TheoryCell;
    var color = PlayerPalette.MainOf(bonus.Owner.Color);
    SpawnFloatingText($"+{bonus.Amount}$", color, IncomeFontSize, CellCenter(cell.ToVector2I()), IncomeStagger);

    var duration = IncomeStagger + IncomeFadeIn + IncomeHold + IncomeFadeOut;
    await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
  }

  /// <summary>
  /// Показать доход теории от адептов: над каждой карточкой стопки и каждым миплом-лидером всплывает,
  /// сколько получил её владелец (цветом владельца), над стопкой — сколько адептов принесли деньги.
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

    var meeples = _leaderMeeples.GetValueOrDefault(column);
    foreach (var meeple in income.Meeples.Where(meeple => meeple.Amount > 0))
    {
      if (meeples is null || meeple.Index >= meeples.Count)
        break;

      var color = PlayerPalette.MainOf(meeple.Owner.Color);
      var above = meeples[meeple.Index].Position - new Vector2(0, MeepleView.Size.Y);
      SpawnFloatingText($"+{meeple.Amount}$", color, IncomeFontSize, above, delay);
      delay += IncomeStagger;
    }

    var duration = delay + IncomeFadeIn + IncomeHold + IncomeFadeOut;
    await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
  }

  /// <summary>
  /// Карточка личного штаба принесла деньги: над ней всплывает сумма цветом игрока, а под суммой зелёным —
  /// бонус учёных, например «(Учёные: +1$)».
  /// </summary>
  private void OnCardTrained(Cell cell) => UpdateSuccessModifiers();

  /// <summary>
  /// Адептов перевели: с одной теории фигурки уходят, на другую — падают.
  /// </summary>
  private void OnAdeptsMoved(TheoryColumn from, TheoryColumn to, int count)
  {
    if (_adepts.TryGetValue(from, out var view))
      view.ShrinkTo(from.Adepts);
    OnAdeptsAdded(to, count);
  }

  /// <summary>
  /// На персонажа в личном штабе положили фишку чипа.
  /// </summary>
  private void OnCharacterChipped(Cell cell)
  {
    if (ViewAt(cell.ToVector2I()) is { } view)
      view.Chipped = true;
  }

  /// <summary>
  /// Адепты ушли из теории: фигурки убывают.
  /// </summary>
  private void OnAdeptsRemoved(TheoryColumn column, int count)
  {
    if (_adepts.TryGetValue(column, out var view))
      view.ShrinkTo(column.Adepts);
    QueueRedraw();
  }

  /// <summary>
  /// Владелец получил карточку теории: она ложится в ряд карточек теорий под его рабочей зоной.
  /// </summary>
  private void OnTheoryCardGranted(OwnedTheoryCard owned)
  {
    var view = CardView.Create(owned.Card);
    view.CardOwner = owned.Owner;
    _theoryViews[owned] = view;
    SnapTo(view, owned.Cell.ToVector2I());
  }

  /// <summary>
  /// Теория перешла новому владельцу: её карточка уезжает к нему в ряд, сохраняя состояние.
  /// </summary>
  private void OnTheoryCardTransferred(OwnedTheoryCard owned)
  {
    if (!_theoryViews.TryGetValue(owned, out var view))
      return;

    view.CardOwner = owned.Owner;
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(owned.Cell.ToVector2I()), SnapDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
  }

  /// <summary>
  /// Карточку теории применили: она переворачивается рубашкой вверх.
  /// </summary>
  private void OnTheoryCardUsed(OwnedTheoryCard owned)
  {
    if (_theoryViews.TryGetValue(owned, out var view))
      view.FlipFaceDown();
  }

  /// <summary>
  /// Обновление карточек: применённые карточки теорий переворачиваются лицевой стороной вверх.
  /// </summary>
  private void OnTheoryCardsRefreshed(IReadOnlyList<OwnedTheoryCard> cards)
  {
    foreach (var owned in cards)
    {
      if (_theoryViews.TryGetValue(owned, out var view))
        view.FlipFaceUp();
    }
  }

  /// <summary>
  /// Карточку забрали из личного штаба в руку: она тает со стола, вместе с адептами на ней.
  /// </summary>
  private void OnCardTakenToHand(Cell cell)
  {
    var key = cell.ToVector2I();
    if (_cardAdepts.Remove(key, out var adepts))
      adepts.QueueFree();
    if (!_views.Remove(key, out var view))
      return;

    var tween = view.CreateTween();
    tween.TweenProperty(view, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, 0.3);
    tween.TweenCallback(Callable.From(view.QueueFree));
    UpdateSuccessModifiers();
    QueueRedraw();
  }

  private void OnCardEarned(Cell cell, int amount, int scientistBonus, int theoryBonus)
  {
    var center = CellCenter(cell.ToVector2I());
    SpawnFloatingText($"+{amount}$", PlayerPalette.MainOf(Session.CurrentPlayer.Color), IncomeFontSize, center, 0);
    var line = 1;
    if (scientistBonus > 0)
      SpawnFloatingText($"(Учёные: +{scientistBonus}$)", ScientistBonusColor, ScientistBonusFontSize, center + new Vector2(0, IncomeFontSize * line++), 0);
    if (theoryBonus > 0)
      SpawnFloatingText($"(Бонусы теорий: +{theoryBonus}$)", ScientistBonusColor, ScientistBonusFontSize, center + new Vector2(0, IncomeFontSize * line), 0);
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

  private void OnFirstPlayerChanged(Player player) =>
    _firstPlayerToken.MoveTo(FirstPlayerTokenPosition(player), animate: true);

  /// <summary>
  /// Жетон первого игрока лежит на левом верхнем углу зоны игрока, наполовину снаружи, чтобы карточки его не закрывали.
  /// </summary>
  private Vector2 FirstPlayerTokenPosition(Player player) =>
    ZoneRect(Field.PlayerZones[player]).Position + new Vector2(FirstPlayerToken.Radius, 0);

  private void OnCommonZoneChanged(Zone zone) => QueueRedraw();

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
  /// Сыгранные карточки личных штабов переворачиваются лицевой стороной вверх, повёрнутые — возвращаются.
  /// </summary>
  private void OnUsedCardsRefreshed(IReadOnlyList<Cell> cells)
  {
    foreach (var cell in cells)
    {
      if (ViewAt(cell.ToVector2I()) is not { } view)
        continue;

      view.FlipFaceUp();
      view.Rotated = false;
    }
  }

  /// <summary>
  /// Показать щит на карточках личных штабов, которые защищает полицейский. Защита меняется, когда карточки
  /// выкладывают, играют и обновляют, поэтому проверяем каждый кадр; карточка перерисовывается, только если щит изменился.
  /// </summary>
  private void UpdateShields()
  {
    foreach (var (cell, view) in _views)
      view.Shielded = Field.IsProtected(cell.ToCell());
  }

  /// <summary>
  /// Показать на всех карточках личных штабов, сыграны ли они: действие могло перевернуть и чужие карточки.
  /// </summary>
  public void ShowPlayStates()
  {
    foreach (var zone in Field.PlayerZones.Values)
    {
      foreach (var cell in zone.Cells)
        ShowPlayState(cell);
    }
  }

  /// <summary>
  /// Показать, сыграна ли карточка в личном штабе: после быстрого действия она повёрнута, после обычного — перевёрнута.
  /// </summary>
  public void ShowPlayState(Cell cell)
  {
    if (ViewAt(cell.ToVector2I()) is not { } view)
      return;

    view.Rotated = Field.IsRotated(cell);
    if (Field.IsUsed(cell))
      view.FlipFaceDown();
  }

  /// <summary>
  /// В теорию поставлен мипл-лидер: он падает под карточку теории, остальные миплы раздвигаются.
  /// </summary>
  /// <summary>
  /// Облик фигурок теории: инопланетяне — в теории «Инопланетяне среди нас» и её подтеориях,
  /// люди в мантиях — в «Тайном правительстве» и её подтеориях.
  /// </summary>
  private static MeepleView.MeepleLook LookOf(TheoryColumn column)
  {
    var root = column.Theory.Parent ?? column.Theory;
    if (root == TheoryCatalog.AliensAmongUs)
      return MeepleView.MeepleLook.Alien;
    return root == TheoryCatalog.SecretGovernment ? MeepleView.MeepleLook.Robed : MeepleView.MeepleLook.Wooden;
  }

  private void OnLeaderMeepleAdded(TheoryColumn column, Player player)
  {
    if (!_leaderMeeples.TryGetValue(column, out var meeples))
      _leaderMeeples[column] = meeples = [];

    var meeple = new MeepleView
    {
      Name = "LeaderMeeple",
      Color = PlayerPalette.MainOf(player.Color),
      Look = LookOf(column),
      Scale = Vector2.One * LeaderMeepleScale,
      Height = LeaderMeepleDropHeight,
    };
    meeples.Add(meeple);
    _adeptsLayer.AddChild(meeple);
    for (var i = 0; i < meeples.Count - 1; i++)
    {
      meeples[i].CreateTween()
        .TweenProperty(meeples[i], Node2D.PropertyName.Position.ToString(), LeaderMeeplePosition(column, i, meeples.Count), LeaderMeepleShiftDuration)
        .SetTrans(Tween.TransitionType.Cubic)
        .SetEase(Tween.EaseType.Out);
    }

    meeple.Position = LeaderMeeplePosition(column, meeples.Count - 1, meeples.Count);
    meeple.CreateTween()
      .TweenProperty(meeple, MeepleView.PropertyName.Height.ToString(), 0f, LeaderMeepleDropDuration)
      .SetTrans(Tween.TransitionType.Bounce)
      .SetEase(Tween.EaseType.Out);
    // Перерисовываем итоги теорий: мипл добавляет влияние.
    QueueRedraw();
  }

  /// <summary>
  /// Где стоит мипл-лидер: в ряд под нижним краем карточки теории.
  /// </summary>
  private static Vector2 LeaderMeeplePosition(TheoryColumn column, int index, int count)
  {
    var center = CellCenter(column.TheoryCell.ToVector2I());
    var step = Mathf.Min(MeepleView.Size.X * LeaderMeepleScale * 1.15f, CardPainter.CardSize.X / count);
    return new Vector2(center.X + (index - (count - 1) / 2f) * step, center.Y + CellSize.Y / 2);
  }

  /// <summary>
  /// Миплов-лидеров сняли: они тают.
  /// </summary>
  private void OnLeaderMeeplesRemoved(IReadOnlyList<TheoryColumn> columns)
  {
    foreach (var column in columns)
    {
      if (!_leaderMeeples.Remove(column, out var meeples))
        continue;

      foreach (var meeple in meeples)
      {
        var tween = meeple.CreateTween();
        tween.TweenProperty(meeple, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, LeaderMeepleFadeDuration);
        tween.TweenCallback(Callable.From(meeple.QueueFree));
      }
    }

    QueueRedraw();
  }

  /// <summary>
  /// Карточку убрали из стопки теории: она тает, а карточки над ней опускаются.
  /// </summary>
  private void OnTheoryCardRemoved(Cell cell, IReadOnlyList<(Cell From, Cell To)> moves)
  {
    if (_views.Remove(cell.ToVector2I(), out var removed))
    {
      var tween = removed.CreateTween();
      tween.TweenProperty(removed, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, 0.3);
      tween.TweenCallback(Callable.From(removed.QueueFree));
    }

    foreach (var (from, to) in moves)
    {
      if (!_views.Remove(from.ToVector2I(), out var view))
        continue;

      _views[to.ToVector2I()] = view;
      SlideTo(view, to.ToVector2I());
    }

    // Перерисовываем итоги теорий: изменились влияние и число человек.
    QueueRedraw();
  }

  /// <summary>
  /// Карточку в ячейке заменили другой: бездомного обучили, лидера закрыли.
  /// </summary>
  private void OnCardReplaced(Cell cell)
  {
    if (ViewAt(cell.ToVector2I()) is { } view && Field.CardAt(cell) is { } card)
    {
      view.ShowCard(card);
      view.Chipped = Field.Chips.ContainsKey(cell);
      // Замененная карточка могла перейти другому игроку: владелец мипла — владелец новой карточки.
      if (Field.ColumnAt(cell) is { } column)
        view.CardOwner = column.OwnerAt(cell);
    }
    // Перерисовываем итоги теорий и колоду рынка: из неё могли взять карточку.
    QueueRedraw();
  }

  /// <summary>
  /// Две карточки теории поменялись местами: они плавно переезжают.
  /// </summary>
  private void OnCardsSwapped(Cell first, Cell second)
  {
    var a = first.ToVector2I();
    var b = second.ToVector2I();
    _views.Remove(a, out var viewA);
    _views.Remove(b, out var viewB);
    if (viewA is not null)
    {
      _views[b] = viewA;
      SlideTo(viewA, b);
    }
    if (viewB is not null)
    {
      _views[a] = viewB;
      SlideTo(viewB, a);
    }

    QueueRedraw();
  }

  /// <summary>
  /// Теорию сдвинули в соседний столбец: её карточки, адепты и миплы-лидеры плавно переезжают.
  /// </summary>
  private void OnTheoryColumnMoved(TheoryColumn column, IReadOnlyList<(Cell From, Cell To)> moves)
  {
    MoveViews(_views, moves);
    MoveViews(_cardAdepts, moves);
    if (_adepts.TryGetValue(column, out var adepts))
      SlideView(adepts, CellCenter(column.TheoryCell.ToVector2I()));
    if (_leaderMeeples.TryGetValue(column, out var meeples))
    {
      for (var i = 0; i < meeples.Count; i++)
        SlideView(meeples[i], LeaderMeeplePosition(column, i, meeples.Count));
    }

    QueueRedraw();
  }

  /// <summary>
  /// Перенести виды по ячейкам: вид, стоявший в <c>From</c>, теперь в <c>To</c>.
  /// </summary>
  private static void MoveViews<T>(Dictionary<Vector2I, T> views, IReadOnlyList<(Cell From, Cell To)> moves)
    where T : Node2D
  {
    var moved = new List<(Vector2I To, T View)>();
    foreach (var (from, to) in moves)
    {
      if (views.Remove(from.ToVector2I(), out var view))
        moved.Add((to.ToVector2I(), view));
    }

    foreach (var (to, view) in moved)
    {
      views[to] = view;
      SlideView(view, CellCenter(to));
    }
  }

  private static void SlideView(Node2D view, Vector2 position) =>
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), position, SnapDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.InOut);

  private static void SlideTo(CardView view, Vector2I cell) =>
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(cell), SnapDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.InOut);

  /// <summary>
  /// Закрытую карточку раскрыли: теперь все видят её лицевую сторону.
  /// </summary>
  private void OnHiddenCardRevealed(Cell cell) => ViewAt(cell.ToVector2I())?.QueueRedraw();

  /// <summary>
  /// Закрытую карточку выложили в стопку теории: она выезжает из-под верхней карточки стопки и отмечается миплом владельца.
  /// </summary>
  private void OnHiddenCardPlaced(Cell cell, Player owner)
  {
    var view = CardView.Create(Field.CardAt(cell)!);
    view.CardOwner = owner;
    view.Position = CellCenter(cell.Below.ToVector2I());
    Place(view, cell.ToVector2I());
  }

  /// <summary>
  /// Карточка теории принесла деньги: сумма всплывает над ней цветом владельца.
  /// </summary>
  private void OnTheoryEarned(OwnedTheoryCard owned, int amount)
  {
    var center = CellCenter(owned.Cell.ToVector2I()) - Vector2.Up * CellSize.Y / 2;
    SpawnFloatingText($"+{amount}$", PlayerPalette.MainOf(owned.Owner.Color), IncomeFontSize, center, 0);
  }

  /// <summary>
  /// Игрок посмотрел в закрытую карточку: её лицевая сторона видна ему <see cref="PeekShowDuration"/> секунды,
  /// дальше — когда он наводит на неё мышь.
  /// </summary>
  private void OnHiddenCardPeeked(Cell cell, Player player)
  {
    if (ViewAt(cell.ToVector2I()) is not { } view)
      return;

    _shownHidden.Add(view);
    GetTree().CreateTimer(PeekShowDuration).Timeout += () => _shownHidden.Remove(view);
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
      view = new AdeptsView { Name = $"Adepts{column.Theory.Name}", Position = CellCenter(column.TheoryCell.ToVector2I()), Look = LookOf(column) };
      _adeptsLayer.AddChild(view);
      _adepts[column] = view;
    }

    view.Add(count);
    // Перерисовываем итоги теорий: под теорией подписано количество адептов.
    QueueRedraw();
  }

  /// <summary>
  /// На карточку в личном штабе пришли адепты: фигурки ставятся на неё, а на значке — сколько их всего.
  /// </summary>
  private void OnCardAdeptsAdded(Cell cell, int count)
  {
    var key = cell.ToVector2I();
    if (!_cardAdepts.TryGetValue(key, out var view))
    {
      view = new AdeptsView { Name = $"Adepts{key.X}_{key.Y}", Position = CellCenter(key) };
      _adeptsLayer.AddChild(view);
      _cardAdepts[key] = view;
    }

    view.Add(count);
    view.Total = Field.AdeptsAt(cell);
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

    // В одном столбце лежит только одна теория.
    foreach (var column in Field.TheoryColumns)
    {
      var area = new Rect2(CellOrigin(new Vector2I(column.TheoryCell.Column, row)), CellSize).Grow(-SlotInset);
      DrawTotals(column, area);
    }
  }

  private void DrawTotals(TheoryColumn column, Rect2 area)
  {
    List<(int FontSize, List<(string Text, Color Color)> Segments)> lines =
    [
      (TotalsFontSize, [("Влияние", TotalsColor)]),
      (TotalsValueFontSize, InfluenceSegments(column)),
      (TotalsFontSize, [("Человек", TotalsColor)]),
      (TotalsValueFontSize, [(column.Cards.Count.ToString(), TotalsColor)]),
      (TotalsFontSize, [("Адептов", TotalsColor)]),
      (TotalsValueFontSize, [(column.Adepts.ToString(), TotalsColor)]),
    ];
    if (column.IsClosed)
      lines.Add((TotalsFontSize, [("Закрыт", ClosedColor)]));
    // Владелец есть у любой теории, в том числе побочной: это игрок с наибольшим влиянием в её стопке.
    var owner = column.Owner;
    lines.Add((TotalsFontSize, [("Владелец", TotalsColor)]));
    lines.Add((TotalsOwnerFontSize, [($"{owner.Name} ({column.InfluenceOf(owner)})", PlayerPalette.MainOf(owner.Color))]));
    // Высота строки пропорциональна размеру шрифта.
    var scale = area.Size.Y / lines.Sum(line => line.FontSize);
    var top = area.Position.Y;
    foreach (var (fontSize, segments) in lines)
    {
      var lineArea = new Rect2(area.Position.X, top, area.Size.X, fontSize * scale);
      CardPainter.DrawSegmentedText(this, segments, lineArea, fontSize);
      top = lineArea.End.Y;
    }
  }

  /// <summary>
  /// Общее влияние теории и в скобках через слеш влияние каждого игрока его цветом: «12 (3/0/5/4)».
  /// </summary>
  private List<(string Text, Color Color)> InfluenceSegments(TheoryColumn column)
  {
    var segments = new List<(string Text, Color Color)> { (column.Influence.ToString(), TotalsColor), (" (", TotalsColor) };
    for (var i = 0; i < Session.Players.Count; i++)
    {
      var player = Session.Players[i];
      if (i > 0)
        segments.Add(("/", TotalsColor));
      segments.Add((column.InfluenceOf(player).ToString(), PlayerPalette.MainOf(player.Color)));
    }
    segments.Add((")", TotalsColor));
    return segments;
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
      if (zone.Contains(cell) || Field.TheoryCardZoneOf(player).Contains(cell))
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
