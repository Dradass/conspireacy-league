using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно «История»: действия игроков по порядку (<see cref="GameHistory"/>), имя — цветом игрока, раунды разделены.
/// Новые записи добавляются снизу, список прокручивается к ним сам, пока его не прокрутили вверх вручную.
/// </summary>
/// <remarks>
/// Не масштабируется под окно (<see cref="IFillsHudWindow"/>): в растянутом окне видно больше записей, а не крупнее шрифт.
/// </remarks>
public partial class HistoryView : RichTextLabel, IFillsHudWindow
{
  private static readonly Vector2 MinimumSize = new(160, 48);
  private static readonly Color RoundColor = UiStyle.Text with { A = 0.6f };

  private bool _hasLines;

  /// <summary>
  /// История партии.
  /// </summary>
  public GameHistory History { get; init; } = null!;

  public Vector2 InitialSize => new(420, 260);

  public override void _Ready()
  {
    BbcodeEnabled = true;
    ScrollActive = true;
    ScrollFollowing = true;
    SelectionEnabled = true;
    CustomMinimumSize = MinimumSize;
    AddThemeFontSizeOverride("normal_font_size", 13);
    AddThemeFontSizeOverride("bold_font_size", 13);

    foreach (var entry in History.Entries)
      Append(entry);
    History.Added += Append;
  }

  public override void _ExitTree() => History.Added -= Append;

  private void Append(HistoryEntry entry)
  {
    if (_hasLines)
      AppendText("\n");
    _hasLines = true;

    var text = Escape(entry.Text);
    if (entry.IsRoundStart)
      AppendText($"[center][color=#{RoundColor.ToHtml()}]— {text} —[/color][/center]");
    else if (entry.Player is { } player)
      AppendText($"[b][color=#{PlayerPalette.MainOf(player.Color).ToHtml(false)}]{Escape(player.Name)}[/color][/b]: {text}");
    else
      AppendText($"[i]{text}[/i]");
  }

  private static string Escape(string text) => text.Replace("[", "[lb]");
}
