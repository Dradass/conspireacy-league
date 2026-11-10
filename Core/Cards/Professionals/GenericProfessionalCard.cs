using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
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
  /// Влияние Бандита.
  /// </summary>
  public const int BanditInfluence = 4;

  /// <summary>
  /// Влияние Политика.
  /// </summary>
  public const int PoliticianInfluence = 5;

  /// <summary>
  /// На сколько $ дешевле Актёру без теории присоединиться к теории.
  /// </summary>
  public const int ActorJoiningDiscount = 10;

  /// <summary>
  /// Профессия.
  /// </summary>
  public Profession Profession { get; }

  /// <param name="profession">Профессия.</param>
  /// <param name="theory">Теория, к которой относится карточка.</param>
  public GenericProfessionalCard(Profession profession, Theory? theory = null)
    : base(
      name: NameOf(profession),
      successChancePercent: SuccessChanceOf(profession),
      cost: CostOf(profession),
      onPlayFromHand: ActionOf(profession),
      onPlacedInHeadquarters: PlacementActionOf(profession),
      passive: PassiveOf(profession),
      theory: theory,
      influence: InfluenceOf(profession))
  {
    Profession = profession;
  }

  /// <summary>
  /// На карточку Мастера теорий в личном штабе можно приводить адептов; сверх приведённых он сам адептов не даёт
  /// (после паса Мастер приводит адептов за соседних профессионалов, см. <see cref="PlayingField.AddMasterAdepts"/>).
  /// </summary>
  public override int? AdeptsBonus => Profession == Profession.TheoryMaster ? 0 : null;

  /// <summary>
  /// Актёр без теории присоединяется к теории со скидкой.
  /// </summary>
  public override int JoiningDiscount => Profession == Profession.Actor && Theory is null ? ActorJoiningDiscount : 0;

  private static int InfluenceOf(Profession profession) => profession switch
  {
    Profession.Bandit => BanditInfluence,
    Profession.Politician => PoliticianInfluence,
    _ => ProfessionalInfluence,
  };

  private static int CostOf(Profession profession) => profession switch
  {
    Profession.Bandit => 7,
    Profession.Driver => 8,
    Profession.Policeman => 9,
    Profession.Journalist => 9,
    Profession.Scientist => 10,
    Profession.Coach => 9,
    Profession.Actor => 12,
    Profession.SpecialAgent => 11,
    Profession.TheoryMaster => 11,
    Profession.Politician => 18,
    _ => 6,
  };

  private static int SuccessChanceOf(Profession profession) => profession switch
  {
    Profession.Scientist => 100,
    Profession.Psychic => 50,
    Profession.Bandit => 75,
    Profession.Driver => 75,
    _ => 85,
  };

  private static PassiveAbility? PassiveOf(Profession profession) => profession switch
  {
    Profession.Scientist => new ScientistPassive(),
    Profession.Policeman => new PolicemanPassive(),
    Profession.TheoryMaster => new TheoryMasterPassive(),
    Profession.Coach => new CoachPassive(),
    _ => null,
  };

  private static Effect PlacementActionOf(Profession profession) => profession switch
  {
    Profession.Psychic => new PeekTheoryEffect(),
    Profession.Bandit => new RobLeadersEffect(),
    Profession.Policeman => new RemoveHomelessEffect(),
    Profession.Journalist => new AddAdeptsPerProfessionalEffect(2),
    Profession.Actor => new AddAdeptsInOwnTheoryEffect(5),
    Profession.SpecialAgent => new CloseLeaderEffect(),
    Profession.Coach => new TrainHomelessEffect(),
    Profession.Driver => new SwapCardsEffect(),
    _ => Effect.None,
  };

  private static Effect ActionOf(Profession profession) => profession switch
  {
    Profession.Scientist => new PlayOwnCardEffect(),
    Profession.Psychic => new ChooseOneEffect(new PeekHiddenCardEffect(), new GainMoneyEffect(6)),
    Profession.Policeman => new InspectionEffect(),
    Profession.Driver => new ChooseOneEffect(new MoveAdeptEffect(), new MarkForReturnEffect(reward: 2)),
    Profession.Coach => new ChooseOneEffect(new TrainEffect(), new ToggleTheoryEffect()),
    Profession.Actor => new ChooseOneEffect(
      new CopyRivalCardEffect(),
      new QuickEffect(new AddAdeptsEffect(2, AdeptDestination.Theory))),
    Profession.SpecialAgent => new ChooseOneEffect(new PlaceHiddenCardEffect(), new FlipRivalCardEffect()),
    Profession.Journalist => new ChooseOneEffect(new AddAdeptsEffect(3, AdeptDestination.Theory), new RevealHiddenCardEffect()),
    Profession.Politician => new GainMoneyEffect(9),
    Profession.TheoryMaster => new GainMoneyPerAdeptEffect(),
    Profession.Bandit => new ChooseOneEffect(new UseRivalCardEffect(), new PlaceLeaderMeepleEffect()),
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
    Profession.Bandit => "Бандит",
    Profession.Politician => "Политик",
    Profession.Driver => "Водитель",
    Profession.TheoryMaster => "Мастер теорий",
    _ => throw new ArgumentOutOfRangeException(nameof(profession), profession, null),
  };
}
