using ConspiracyLeague.Core.Field;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно перед началом партии: количество игроков и строка с именем для каждого. «Начать» запускает партию,
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
  private string _title = string.Empty;
  private TaskCompletionSource<IReadOnlyList<string>?> _result = null!;
  private Label _countLabel = null!;
  private HSlider _count = null!;
  private VBoxContainer _rows = null!;

  /// <summary>
  /// Показать окно поверх <paramref name="parent"/> и дождаться решения игроков.
  /// </summary>
  /// <param name="title">Заголовок окна: какую партию начинают.</param>
  /// <returns>Имена игроков по порядку или <c>null</c>, если нажали «Назад».</returns>
  public static Task<IReadOnlyList<string>?> Ask(Node parent, string title)
  {
    var dialog = new PlayersDialog { Name = "PlayersDialog", _title = title };
    dialog._result = new TaskCompletionSource<IReadOnlyList<string>?>();
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

    column.AddChild(CreateLabel("Имена игроков"));
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
      field.TextSubmitted += _ => Start();
      _rows.AddChild(field);
      _fields.Add(field);
    }

    while (_fields.Count > Count)
    {
      var last = _fields[^1];
      _fields.RemoveAt(_fields.Count - 1);
      _rows.RemoveChild(last);
      last.QueueFree();
    }
  }

  private void Start()
  {
    // Пустое имя — имя по умолчанию «Игрок N».
    var names = _fields
      .Select(field => field.Text.Trim() is { Length: > 0 } name ? name : field.DefaultName)
      .ToList();
    _result.TrySetResult(names);
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
    public string DefaultName { get; init; } = string.Empty;

    public override void _GuiInput(InputEvent @event)
    {
      if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Text == DefaultName)
        Text = string.Empty;
      base._GuiInput(@event);
    }
  }
}
