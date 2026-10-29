using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Professionals;

/// <summary>
/// Профессионал без особых свойств профессии.
/// </summary>
/// <remarks>
/// TODO: временная карточка, пока у профессий нет своих наследников <see cref="ProfessionalCard"/>
/// с настоящими действиями и стоимостью.
/// </remarks>
public sealed class GenericProfessionalCard : ProfessionalCard
{
  /// <summary>
  /// Профессия.
  /// </summary>
  public Profession Profession { get; }

  /// <param name="profession">Профессия.</param>
  /// <param name="theory">Теория, к которой относится карточка.</param>
  public GenericProfessionalCard(Profession profession, Theory? theory = null)
    : base(
      name: NameOf(profession),
      successChancePercent: 60,
      cost: 6,
      onPlayFromHand: ActionOf(profession),
      onPlacedInHeadquarters: Effect.None,
      theory: theory)
  {
    Profession = profession;
  }

  private static Effect ActionOf(Profession profession) => profession switch
  {
    Profession.SpecialAgent => new ChooseOneEffect(new PlaceHiddenCardEffect(), new FlipRivalCardEffect()),
    _ => new GainMoneyEffect(4),
  };

  private static string NameOf(Profession profession) => profession switch
  {
    Profession.Journalist => "Журналист",
    Profession.Actor => "Актёр",
    Profession.SpecialAgent => "Спецагент",
    Profession.Policeman => "Полицейский",
    Profession.Scientist => "Учёный",
    Profession.Psychic => "Экстрасенс",
    Profession.Coach => "Тренер",
    Profession.CrimeBoss => "Авторитет",
    Profession.Politician => "Политик",
    Profession.Driver => "Водитель",
    Profession.TheoryMaster => "Мастер теорий",
    _ => throw new ArgumentOutOfRangeException(nameof(profession), profession, null),
  };
}
