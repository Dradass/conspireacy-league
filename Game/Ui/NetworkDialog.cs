using ConspiracyLeague.Game.Net;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно «Сетевая игра»: имя игрока и переключатель «Интернет / Локальная сеть». Через интернет подключаются к отдельному
/// серверу (адрес и, если сертификат самоподписанный, его отпечаток). В локальной сети можно создать стол на этом
/// компьютере или подключиться к столу соседа. Видны только поля выбранного режима. Возвращает готовое подключение
/// (<see cref="NetworkConnection"/>).
/// </summary>
public partial class NetworkDialog : MenuDialog
{
  private const int FieldFontSize = 20;
  private const float FieldHeight = 36f;
  private const float CaptionWidth = 115f;
  private const float ActionWidth = 180f;

  /// <summary>
  /// Ширина содержимого: в строке помещаются подпись, адрес вида «192.168.100.200:7777» и кнопка.
  /// </summary>
  private const float ContentWidth = 600f;
  private static readonly Color ErrorColor = new("ff7a5c");

  private TaskCompletionSource<NetworkConnection?> _result = null!;
  private LineEdit _name = null!;
  private Button _internetTab = null!;
  private Button _lanTab = null!;
  private Control _internet = null!;
  private Control _lan = null!;
  private LineEdit _internetAddress = null!;
  private LineEdit _fingerprint = null!;
  private LineEdit _lanAddress = null!;
  private SpinBox _port = null!;
  private Label _error = null!;

  /// <summary>
  /// Показать окно поверх <paramref name="parent"/> и дождаться подключения.
  /// </summary>
  /// <returns>Подключение или <c>null</c>, если нажали «Назад».</returns>
  public static Task<NetworkConnection?> Ask(Node parent)
  {
    var dialog = new NetworkDialog { Name = "NetworkDialog", _result = new TaskCompletionSource<NetworkConnection?>() };
    parent.AddChild(dialog);
    return dialog._result.Task;
  }

  public override void _Ready()
  {
    var column = BuildFrame("Сетевая игра");
    column.CustomMinimumSize = new Vector2(ContentWidth, 0);

    // Переключатель режима: две кнопки, нажата всегда одна.
    var group = new ButtonGroup();
    var tabs = new HBoxContainer { Name = "Mode" };
    tabs.AddThemeConstantOverride("separation", 8);
    _internetTab = CreateTab("Internet", "Интернет", group);
    _lanTab = CreateTab("Lan", "Локальная сеть", group);
    tabs.AddChild(_internetTab);
    tabs.AddChild(_lanTab);
    column.AddChild(tabs);

    _name = CreateField("Name", GameOptions.PlayerName);
    column.AddChild(Row("Имя", _name));

    _internet = BuildInternet();
    column.AddChild(_internet);
    _lan = BuildLan();
    column.AddChild(_lan);

    _error = CreateLabel(string.Empty, 18);
    _error.AddThemeColorOverride("font_color", ErrorColor);
    _error.Visible = false;
    column.AddChild(_error);
    column.AddChild(CreateButton("Back", "Назад", Cancel));

    group.Pressed += _ => ShowMode();
    _internetTab.ButtonPressed = true;
    ShowMode();
  }

  /// <summary>
  /// Интернет: адрес отдельного сервера и отпечаток его сертификата.
  /// </summary>
  private Control BuildInternet()
  {
    var box = Section("InternetSection");
    _internetAddress = CreateField("InternetAddress", GameOptions.InternetAddress);
    _internetAddress.PlaceholderText = "wss://сервер:7777";
    _internetAddress.TextSubmitted += _ => JoinInternet();
    box.AddChild(Row("Сервер", _internetAddress));
    _fingerprint = CreateField("Fingerprint", GameOptions.Fingerprint);
    _fingerprint.PlaceholderText = "необязательно";
    box.AddChild(Row("Отпечаток", _fingerprint));
    box.AddChild(CreateButton("JoinInternet", "Подключиться", JoinInternet));
    return box;
  }

  /// <summary>
  /// Локальная сеть: создать стол на этом компьютере или подключиться к столу по адресу в сети.
  /// </summary>
  private Control BuildLan()
  {
    var box = Section("LanSection");
    _port = new SpinBox
    {
      Name = "Port",
      MinValue = 1024,
      MaxValue = 65535,
      Step = 1,
      Value = GameOptions.Port,
      Alignment = HorizontalAlignment.Center,
      CustomMinimumSize = new Vector2(0, FieldHeight),
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };
    _port.GetLineEdit().AddThemeFontSizeOverride("font_size", FieldFontSize);
    box.AddChild(Row("Порт", _port, Action("Host", "Создать стол", Host)));

    _lanAddress = CreateField("LanAddress", GameOptions.Address);
    _lanAddress.PlaceholderText = "192.168.1.15:7777";
    _lanAddress.TextSubmitted += _ => JoinLan();
    box.AddChild(Row("Адрес", _lanAddress, Action("JoinLan", "Подключиться", JoinLan)));
    return box;
  }

  private void ShowMode()
  {
    var internet = _internetTab.ButtonPressed;
    _internet.Visible = internet;
    _lan.Visible = !internet;
    _error.Visible = false;
  }

  private static VBoxContainer Section(string name)
  {
    var box = new VBoxContainer { Name = name };
    box.AddThemeConstantOverride("separation", 10);
    return box;
  }

  private static Button CreateTab(string name, string text, ButtonGroup group)
  {
    var tab = new Button
    {
      Name = name,
      Text = text,
      ToggleMode = true,
      ButtonGroup = group,
      FocusMode = FocusModeEnum.None,
      CustomMinimumSize = new Vector2(0, FieldHeight + 4),
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };
    tab.AddThemeFontSizeOverride("font_size", FieldFontSize);
    return tab;
  }

  /// <summary>
  /// Строка: подпись слева, поле, и, если есть, кнопка справа.
  /// </summary>
  private static HBoxContainer Row(string caption, Control field, Control? action = null)
  {
    var row = new HBoxContainer();
    row.AddThemeConstantOverride("separation", 8);
    var label = new Label { Text = caption, CustomMinimumSize = new Vector2(CaptionWidth, 0), VerticalAlignment = VerticalAlignment.Center };
    label.AddThemeFontSizeOverride("font_size", FieldFontSize);
    row.AddChild(label);
    row.AddChild(field);
    if (action is not null)
      row.AddChild(action);
    return row;
  }

  private static Button Action(string name, string text, Action pressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      FocusMode = FocusModeEnum.None,
      CustomMinimumSize = new Vector2(ActionWidth, FieldHeight),
    };
    button.AddThemeFontSizeOverride("font_size", FieldFontSize);
    button.Pressed += pressed;
    return button;
  }

  private static LineEdit CreateField(string name, string text)
  {
    var field = new LineEdit
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(0, FieldHeight),
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };
    field.AddThemeFontSizeOverride("font_size", FieldFontSize);
    return field;
  }

  private string PlayerName => _name.Text.Trim() is { Length: > 0 } name ? name : "Игрок";

  private void ShowError(string text)
  {
    _error.Text = text;
    _error.Visible = true;
  }

  private void Host()
  {
    var port = (int)_port.Value;
    try
    {
      Finish(NetworkConnection.Host(port, PlayerName));
      GameOptions.Port = port;
      GameOptions.Save();
    }
    catch (System.Net.Sockets.SocketException error)
    {
      ShowError($"Не удалось открыть порт {port}: {error.Message}");
    }
  }

  private void JoinLan()
  {
    try
    {
      Finish(NetworkConnection.Join(_lanAddress.Text, PlayerName, secure: false));
      GameOptions.Address = _lanAddress.Text.Trim();
      GameOptions.Save();
    }
    catch (ArgumentException error)
    {
      ShowError(error.Message);
    }
  }

  private void JoinInternet()
  {
    try
    {
      var fingerprint = _fingerprint.Text.Trim();
      Finish(NetworkConnection.Join(_internetAddress.Text, PlayerName, secure: true, fingerprint.Length > 0 ? fingerprint : null));
      GameOptions.InternetAddress = _internetAddress.Text.Trim();
      GameOptions.Fingerprint = fingerprint;
      GameOptions.Save();
    }
    catch (ArgumentException error)
    {
      ShowError(error.Message);
    }
  }

  private void Finish(NetworkConnection connection)
  {
    GameOptions.PlayerName = PlayerName;
    _result.TrySetResult(connection);
    QueueFree();
  }

  protected override void Cancel()
  {
    _result.TrySetResult(null);
    QueueFree();
  }
}
