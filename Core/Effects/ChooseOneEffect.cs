using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок выбирает одно из действий: «Получить 3$, ЛИБО добавить 1 адепта в теорию».
/// </summary>
public sealed class ChooseOneEffect : Effect
{
  /// <summary>
  /// Варианты на выбор.
  /// </summary>
  public IReadOnlyList<Effect> Options { get; }

  public ChooseOneEffect(params Effect[] options)
  {
    if (options.Length < 2)
      throw new ArgumentException("Нужно хотя бы два варианта.", nameof(options));
    Options = options;
  }

  public override string Description => string.Join(", ЛИБО ", Options.Select(e => e.Description));

  public override void Execute(GameContext context)
  {
    // TODO: запросить выбор у игрока через контекст (UI или ИИ) и выполнить выбранный вариант.
    throw new NotImplementedException();
  }
}
