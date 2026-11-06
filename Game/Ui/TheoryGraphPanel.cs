using ConspiracyLeague.Core.Theories;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Граф всех теорий: основные теории в верхней строке, их побочные теории — ниже, линиями от основной.
/// Колесо мыши — масштаб, зажатая правая кнопка — перемещение по графу во все стороны.
/// При наведении на теорию показывается её эффект. «Назад» закрывает граф и возвращает в меню.
/// </summary>
public partial class TheoryGraphPanel : Control
{
  /// <summary>
  /// Нажата «Назад»: граф закрыт, нужно вернуть меню.
  /// </summary>
  public event Action? BackRequested;

  private const float NodeWidth = 240f;
  private const float NodeHeight = 64f;
  private const float ColumnGap = 24f;
  private const float GroupGap = 56f;
  private const float RowGap = 72f;
  private const float BandGap = 56f;
  private const float WrapWidth = 1800f;
  private const float LineWidth = 3f;
  private const int NodeFontSize = 18;
  private const int TitleFontSize = 28;
  private const int TooltipWidth = 380;
  private const int Margin = 40;
  private const int HeaderInset = 16;
  private const int HeaderHeight = 64;
  private static readonly Color PanelColor = new(0.08f, 0.1f, 0.09f, 0.95f);
  private const float ZoomStep = 1.15f;
  private const float MinZoom = 0.3f;
  private const float MaxZoom = 2.5f;
  private static readonly Vector2 WorldStart = new(40, 40);

  private static readonly Color ParentBorder = new("e0b84f");
  private static readonly Color ChildBorder = new("6f9c80");
  private static readonly Color NodeBackground = new("1d2b24");
  private static readonly Color LineColor = new("6f9c80");
  private static readonly Vector2 TooltipOffset = new(18, 18);

  private readonly List<(Vector2 From, Vector2 To)> _edges = [];
  private Control _view = null!;
  private ColorRect _dim = null!;
  private ColorRect _frame = null!;
  private Control _world = null!;
  private float _zoom = 1f;
  private bool _panning;
  private PanelContainer _tooltip = null!;
  private RichTextLabel _tooltipText = null!;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Stop;
    Visible = false;

    _dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
    AddChild(_dim);

    _frame = new ColorRect { Color = PanelColor, MouseFilter = MouseFilterEnum.Stop };
    AddChild(_frame);

    var header = new HBoxContainer { Position = new Vector2(Margin + HeaderInset, Margin + HeaderInset) };
    header.AddThemeConstantOverride("separation", 16);
    AddChild(header);

    var back = new Button { Text = "Назад", FocusMode = FocusModeEnum.None };
    back.Pressed += () =>
    {
      Close();
      BackRequested?.Invoke();
    };
    header.AddChild(back);

    var title = new Label { Text = "Теории", VerticalAlignment = VerticalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    header.AddChild(title);

    // Область графа: всё, что вышло за её края, обрезается. Колесо и перетаскивание работают внутри неё.
    _view = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
    _view.GuiInput += OnViewInput;
    AddChild(_view);

    _world = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = WorldStart };
    _world.Draw += DrawEdges;
    _view.AddChild(_world);
    BuildGraph();

    _tooltipText = new RichTextLabel
    {
      BbcodeEnabled = true,
      FitContent = true,
      CustomMinimumSize = new Vector2(TooltipWidth, 0),
      MouseFilter = MouseFilterEnum.Ignore,
    };
    _tooltip = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
    _tooltip.AddChild(_tooltipText);
    AddChild(_tooltip);
  }

  public override void _Process(double delta)
  {
    if (!Visible)
      return;

    // Размер окна берём из экрана: якоря у вложенных элементов здесь не давали им размера.
    Layout();
    if (_tooltip.Visible)
      UpdateTooltipPosition();
  }

  /// <summary>
  /// Показать граф теорий.
  /// </summary>
  public void Open()
  {
    Layout();
    Visible = true;
  }

  /// <summary>
  /// Разместить затемнение, окно и область графа по размеру экрана.
  /// </summary>
  private void Layout()
  {
    var screen = GetViewportRect().Size;
    Position = Vector2.Zero;
    Size = screen;
    _dim.Position = Vector2.Zero;
    _dim.Size = screen;
    _frame.Position = Vector2.One * Margin;
    _frame.Size = screen - Vector2.One * Margin * 2;
    _view.Position = new Vector2(Margin + HeaderInset, Margin + HeaderHeight);
    _view.Size = screen - new Vector2((Margin + HeaderInset) * 2, Margin + HeaderHeight + Margin + HeaderInset);
  }

  /// <summary>
  /// Закрыть граф.
  /// </summary>
  public void Close()
  {
    _tooltip.Visible = false;
    _panning = false;
    Visible = false;
  }

  /// <summary>
  /// Колесо — масштаб вокруг курсора; зажатая правая кнопка — перемещение по графу.
  /// </summary>
  private void OnViewInput(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
        Zoom(ZoomStep, wheel.Position);
        break;
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
        Zoom(1 / ZoomStep, wheel.Position);
        break;
      case InputEventMouseButton { ButtonIndex: MouseButton.Right } button:
        _panning = button.Pressed;
        break;
      case InputEventMouseMotion motion when _panning && (motion.ButtonMask & MouseButtonMask.Right) != 0:
        _world.Position += motion.Relative;
        break;
      case InputEventMouseMotion:
        // Правую кнопку отпустили вне графа — перемещение заканчиваем.
        _panning = false;
        break;
    }
  }

  /// <summary>
  /// Изменить масштаб так, чтобы точка под курсором осталась на месте.
  /// </summary>
  private void Zoom(float factor, Vector2 pointer)
  {
    var zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
    var worldPoint = (pointer - _world.Position) / _zoom;
    _zoom = zoom;
    _world.Scale = Vector2.One * _zoom;
    _world.Position = pointer - worldPoint * _zoom;
  }

  /// <summary>
  /// Разложить теории: каждая основная теория с побочными — группа; группы идут слева направо и переносятся на новую строку.
  /// Линии от основной теории ведут к каждой побочной.
  /// </summary>
  private void BuildGraph()
  {
    var cursorX = 0f;
    var bandTop = 0f;
    var width = 0f;
    var height = 0f;

    foreach (var parent in TheoryCatalog.Parents)
    {
      var children = TheoryCatalog.ChildrenOf(parent).ToList();
      var columns = Math.Max(1, children.Count);
      var groupWidth = columns * NodeWidth + (columns - 1) * ColumnGap;
      if (cursorX > 0 && cursorX + groupWidth > WrapWidth)
      {
        cursorX = 0;
        bandTop += NodeHeight + RowGap + NodeHeight + BandGap;
      }

      var parentPosition = new Vector2(cursorX + groupWidth / 2 - NodeWidth / 2, bandTop);
      AddNode(parent, parentPosition, isParent: true);
      for (var index = 0; index < children.Count; index++)
      {
        var childPosition = new Vector2(cursorX + index * (NodeWidth + ColumnGap), bandTop + NodeHeight + RowGap);
        AddNode(children[index], childPosition, isParent: false);
        _edges.Add((parentPosition + new Vector2(NodeWidth / 2, NodeHeight), childPosition + new Vector2(NodeWidth / 2, 0)));
      }

      cursorX += groupWidth + GroupGap;
      width = Math.Max(width, cursorX - GroupGap);
      height = Math.Max(height, bandTop + 2 * NodeHeight + RowGap);
    }

    _world.Size = new Vector2(width, height);
  }

  private void AddNode(Theory theory, Vector2 position, bool isParent)
  {
    var style = new StyleBoxFlat { BgColor = NodeBackground, BorderColor = isParent ? ParentBorder : ChildBorder };
    style.SetBorderWidthAll(2);
    style.SetCornerRadiusAll(8);
    style.SetContentMarginAll(8);

    var node = new PanelContainer
    {
      Position = position,
      CustomMinimumSize = new Vector2(NodeWidth, NodeHeight),
      Size = new Vector2(NodeWidth, NodeHeight),
      MouseFilter = MouseFilterEnum.Stop,
    };
    node.AddThemeStyleboxOverride("panel", style);

    var label = new Label
    {
      Text = theory.Name,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      AutowrapMode = TextServer.AutowrapMode.WordSmart,
      MouseFilter = MouseFilterEnum.Ignore,
    };
    label.AddThemeFontSizeOverride("font_size", NodeFontSize);
    node.AddChild(label);

    node.MouseEntered += () => ShowTooltip(theory);
    node.MouseExited += () => _tooltip.Visible = false;
    _world.AddChild(node);
  }

  private void DrawEdges()
  {
    foreach (var (from, to) in _edges)
    {
      var middleY = from.Y + RowGap / 2;
      _world.DrawLine(from, new Vector2(from.X, middleY), LineColor, LineWidth);
      _world.DrawLine(new Vector2(from.X, middleY), new Vector2(to.X, middleY), LineColor, LineWidth);
      _world.DrawLine(new Vector2(to.X, middleY), to, LineColor, LineWidth);
    }
  }

  private void ShowTooltip(Theory theory)
  {
    _tooltipText.Text = $"[b]{theory.Name}[/b]\n{CardInfoText.TheoryEffectSummary(theory)}";
    _tooltip.Visible = true;
    UpdateTooltipPosition();
  }

  private void UpdateTooltipPosition()
  {
    var position = GetLocalMousePosition() + TooltipOffset;
    var maxPosition = Size - _tooltip.Size;
    _tooltip.Position = maxPosition.X > 0 && maxPosition.Y > 0 ? position.Clamp(Vector2.Zero, maxPosition) : position;
  }
}
