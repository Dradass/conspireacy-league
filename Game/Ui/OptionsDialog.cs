using ConspiracyLeague.Core.Field;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно «Опции» стартового меню: вид поля (классический или круг), критический провал, скорость ботов и слежение
/// камеры за ботами. Изменения сразу
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

    var speedLabel = CreateLabel(BotSpeedText());
    column.AddChild(speedLabel);
    var speed = new HSlider
    {
      Name = "BotSpeed",
      MinValue = 0,
      MaxValue = GameOptions.BotSlowdowns.Length - 1,
      Step = 1,
      TickCount = GameOptions.BotSlowdowns.Length,
      TicksOnBorders = true,
      Value = GameOptions.BotSpeed,
      FocusMode = FocusModeEnum.None,
      CustomMinimumSize = new Vector2(0, 28),
    };
    speed.ValueChanged += value =>
    {
      GameOptions.BotSpeed = (int)value;
      GameOptions.Save();
      speedLabel.Text = BotSpeedText();
    };
    column.AddChild(speed);

    var follow = new CheckButton
    {
      Name = "FollowBots",
      Text = "Отслеживать движения ботов",
      ButtonPressed = GameOptions.FollowBots,
      FocusMode = FocusModeEnum.None,
    };
    follow.AddThemeFontSizeOverride("font_size", LabelFontSize);
    follow.Toggled += pressed =>
    {
      GameOptions.FollowBots = pressed;
      GameOptions.Save();
    };
    column.AddChild(follow);

    var followHint = CreateLabel("Камера следует за действиями ботов: куда бот кладёт карточку и на что действует.", HintFontSize);
    followHint.AddThemeColorOverride("font_color", HintColor);
    column.AddChild(followHint);

    var incomeAnimation = new CheckButton
    {
      Name = "IncomeAnimation",
      Text = "Анимация получения денег",
      ButtonPressed = GameOptions.IncomeAnimation,
      FocusMode = FocusModeEnum.None,
    };
    incomeAnimation.AddThemeFontSizeOverride("font_size", LabelFontSize);
    incomeAnimation.Toggled += pressed =>
    {
      GameOptions.IncomeAnimation = pressed;
      GameOptions.Save();
    };
    column.AddChild(incomeAnimation);

    var incomeHint = CreateLabel("В конце раунда камера показывает каждую теорию и сколько денег принесли её адепты.", HintFontSize);
    incomeHint.AddThemeColorOverride("font_color", HintColor);
    column.AddChild(incomeHint);

    column.AddChild(CreateButton("Back", "Назад", Cancel));
  }

  /// <summary>
  /// Подпись ползунка скорости ботов с текущим замедлением.
  /// </summary>
  private static string BotSpeedText()
  {
    var slowdown = GameOptions.BotSlowdown;
    return slowdown <= 1 ? "Скорость ботов: быстро" : $"Скорость ботов: в {slowdown:0.#} раза медленнее";
  }
}
