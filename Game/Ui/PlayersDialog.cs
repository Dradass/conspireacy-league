using ConspiracyLeague.Core.Field;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Игрок, заданный перед партией: имя и играет ли за него бот.
/// </summary>
public sealed record PlayerSetup(string Name, bool IsBot);

/// <summary>
/// Окно перед началом партии: количество игроков и строка с именем и галочкой «Бот» для каждого. «Начать» запускает партию,
/// «Назад» (или Esc) возвращает в стартовое меню.
/// </summary>
public partial class PlayersDialog : MenuDialog
{
  /// <summary>
  /// Наименьшее количество игроков в партии.
  /// </summary>
  public const int MinPlayers = 2;

  private const int DefaultPlayers = 4;
  private const int FieldFontSize = 20;
  private const float FieldHeight = 34f;

  private readonly List<NameField> _fields = [];
  private readonly List<CheckBox> _bots = [];
  private string _title = string.Empty;
  private TaskCompletionSource<IReadOnlyList<PlayerSetup>?> _result = null!;
  private Label _countLabel = null!;
  private HSlider _count = null!;
  private VBoxContainer _rows = null!;

  /// <summary>
  /// Показать окно поверх <paramref name="parent"/> и дождаться решения игроков.
  /// </summary>
  /// <param name="title">Заголовок окна: какую партию начинают.</param>
  /// <returns>Игроки по порядку (имя и бот ли это) или <c>null</c>, если нажали «Назад».</returns>
  public static Task<IReadOnlyList<PlayerSetup>?> Ask(Node parent, string title)
  {
    var dialog = new PlayersDialog { Name = "PlayersDialog", _title = title };
    dialog._result = new TaskCompletionSource<IReadOnlyList<PlayerSetup>?>();
    parent.AddChild(dialog);
    return dialog._result.Task;
  }

  public override void _Ready()
  {
    var column = BuildFrame(_title);

    _countLabel = CreateLabel(string.Empty);
    column.AddChild(_countLabel);
    _count = new HSlider
    {
      Name = "Players",
      MinValue = MinPlayers,
      MaxValue = FieldLayout.MaxPlayers,
      Step = 1,
      Value = DefaultPlayers,
      TickCount = FieldLayout.MaxPlayers - MinPlayers + 1,
      TicksOnBorders = true,
      FocusMode = FocusModeEnum.None,
    };
    _count.ValueChanged += _ => UpdateRows();
    column.AddChild(_count);

    column.AddChild(CreateLabel("Имена игроков (галочка — играет бот)"));
    _rows = new VBoxContainer { Name = "Names" };
    _rows.AddThemeConstantOverride("separation", 6);
    column.AddChild(_rows);

    var buttons = new HBoxContainer();
    buttons.AddThemeConstantOverride("separation", 12);
    buttons.AddChild(CreateButton("Back", "Назад", Cancel));
    buttons.AddChild(CreateButton("Start", "Начать", Start));
    column.AddChild(buttons);

    UpdateRows();
  }

  private int Count => (int)_count.Value;

  /// <summary>
  /// Строк для имён столько же, сколько игроков. Уже введённые имена сохраняются, когда количество меняют.
  /// </summary>
  private void UpdateRows()
  {
    _countLabel.Text = $"Количество игроков: {Count}";
    while (_fields.Count < Count)
    {
      var number = _fields.Count + 1;
      var field = new NameField
      {
        Name = $"Name{number}",
        DefaultName = $"Игрок {number}",
        Text = $"Игрок {number}",
        Alignment = HorizontalAlignment.Center,
        CustomMinimumSize = new Vector2(0, FieldHeight),
      };
      field.AddThemeFontSizeOverride("font_size", FieldFontSize);
      field.SizeFlagsHorizontal = SizeFlags.ExpandFill;
      field.TextSubmitted += _ => Start();

      var bot = new CheckBox { Name = $"Bot{number}", Text = "Бот", FocusMode = FocusModeEnum.None };
      bot.AddThemeFontSizeOverride("font_size", FieldFontSize);
      bot.Toggled += pressed => OnBotToggled(field, number, pressed);

      var row = new HBoxContainer { Name = $"Player{number}" };
      row.AddThemeConstantOverride("separation", 8);
      row.AddChild(field);
      row.AddChild(bot);
      _rows.AddChild(row);
      _fields.Add(field);
      _bots.Add(bot);
    }

    while (_fields.Count > Count)
    {
      var row = _fields[^1].GetParent();
      _fields.RemoveAt(_fields.Count - 1);
      _bots.RemoveAt(_bots.Count - 1);
      _rows.RemoveChild(row);
      row.QueueFree();
    }
  }

  /// <summary>
  /// Имя по умолчанию подсказывает, кто играет: «Бот N» или «Игрок N». Введённое вручную имя не трогаем.
  /// </summary>
  private static void OnBotToggled(NameField field, int number, bool bot)
  {
    var wasDefault = field.Text.Trim().Length == 0 || field.Text == field.DefaultName;
    field.DefaultName = bot ? $"Бот {number}" : $"Игрок {number}";
    if (wasDefault)
      field.Text = field.DefaultName;
  }

  private void Start()
  {
    // Пустое имя — имя по умолчанию «Игрок N» («Бот N»).
    var players = _fields
      .Select((field, index) => new PlayerSetup(
        field.Text.Trim() is { Length: > 0 } name ? name : field.DefaultName,
        _bots[index].ButtonPressed))
      .ToList();
    _result.TrySetResult(players);
    QueueFree();
  }

  protected override void Cancel()
  {
    _result.TrySetResult(null);
    QueueFree();
  }

  /// <summary>
  /// Поле имени: по щелчку на имени по умолчанию оно стирается, чтобы игрок сразу начал печатать.
  /// </summary>
  private partial class NameField : LineEdit
  {
    /// <summary>
    /// Имя по умолчанию, которое стирается при щелчке и подставляется, если поле оставили пустым.
    /// </summary>
    public string DefaultName { get; set; } = string.Empty;

    public override void _GuiInput(InputEvent @event)
    {
      if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Text == DefaultName)
        Text = string.Empty;
      base._GuiInput(@event);
    }
  }
}
