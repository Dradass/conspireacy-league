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
  /// На карточку Мастера теорий в личном штабе можно приводить адептов, и каждый раз приходит на одного больше.
  /// </summary>
  public override int? AdeptsBonus => Profession == Profession.TheoryMaster ? 1 : null;

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
    Profession.Driver => 7,
    Profession.Policeman => 9,
    Profession.Journalist => 9,
    Profession.Scientist => 9,
    Profession.Coach => 9,
    Profession.Actor => 11,
    Profession.SpecialAgent => 11,
    Profession.TheoryMaster => 12,
    Profession.Politician => 15,
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
    Profession.Psychic => new ChooseOneEffect(new PeekHiddenCardEffect(), new GainMoneyEffect(7)),
    Profession.Policeman => new InspectionEffect(),
    Profession.Driver => new ChooseOneEffect(new MoveAdeptEffect(), new TakeToHandEffect()),
    Profession.Coach => new ChooseOneEffect(new TrainEffect(), new OpenMarketCardsEffect(2, 3)),
    Profession.Actor => new ChooseOneEffect(
      new CopyRivalCardEffect(),
      new QuickEffect(new AddAdeptsEffect(2, AdeptDestination.Theory))),
    Profession.SpecialAgent => new ChooseOneEffect(new PlaceHiddenCardEffect(), new FlipRivalCardEffect()),
    Profession.Journalist => new ChooseOneEffect(new AddAdeptsEffect(3, AdeptDestination.Theory), new RevealHiddenCardEffect()),
    Profession.Politician => new GainMoneyEffect(10),
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
