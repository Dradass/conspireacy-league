using ConspiracyLeague.Core.Field;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно «Опции» стартового меню: вид поля (классический или круг) и критический провал. Изменения сразу
/// сохраняются (<see cref="GameOptions"/>) и действуют для следующих партий.
/// </summary>
public partial class OptionsDialog : MenuDialog
{
  private const int HintFontSize = 16;
  private const int ClassicView = 0;
  private const int RadialView = 1;

  private static readonly Color HintColor = new("b8c7bd");

  /// <summary>
  /// Показать окно поверх <paramref name="parent"/>.
  /// </summary>
  public static void Open(Node parent) => parent.AddChild(new OptionsDialog { Name = "OptionsDialog" });

  public override void _Ready()
  {
    var column = BuildFrame("Опции");

    column.AddChild(CreateLabel("Вид поля"));
    var view = new OptionButton { Name = "FieldView", FocusMode = FocusModeEnum.None };
    view.AddThemeFontSizeOverride("font_size", LabelFontSize);
    view.AddItem("Классический", ClassicView);
    view.AddItem("Круг", RadialView);
    view.Selected = view.GetItemIndex(GameOptions.RadialField ? RadialView : ClassicView);
    view.ItemSelected += index =>
    {
      GameOptions.RadialField = view.GetItemId((int)index) == RadialView;
      GameOptions.Save();
    };
    column.AddChild(view);

    var critical = new CheckButton
    {
      Name = "CriticalFailure",
      Text = "Критический провал",
      ButtonPressed = GameOptions.CriticalFailure,
      FocusMode = FocusModeEnum.None,
    };
    critical.AddThemeFontSizeOverride("font_size", LabelFontSize);
    critical.Toggled += pressed =>
    {
      GameOptions.CriticalFailure = pressed;
      GameOptions.Save();
    };
    column.AddChild(critical);

    var hint = CreateLabel(
      $"При провале проверки на 1–{PlayingField.HomelessDestroyDie} бездомный уходит из игры, "
      + $"на {PlayingField.CitizenDemoteDie} гражданин становится бездомным.",
      HintFontSize);
    hint.AddThemeColorOverride("font_color", HintColor);
    column.AddChild(hint);

    column.AddChild(CreateButton("Back", "Назад", Cancel));
  }
}
