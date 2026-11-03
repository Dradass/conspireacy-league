using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточки теорий, которые получают владельцы теорий (<see cref="Field.PlayingField"/>).
/// </summary>
/// <remarks>
/// Новая карточка теории добавляется сюда.
/// </remarks>
public static class TheoryCardCatalog
{
  /// <summary>
  /// Новая карточка теории для указанной теории или <c>null</c>, если у теории карточки нет.
  /// </summary>
  public static TheoryCard? Create(Theory theory)
  {
    if (theory == TheoryCatalog.GoldenBillion)
      return new GoldenBillionCard();
    if (theory == TheoryCatalog.CovidHoax)
      return new CovidHoaxCard();
    if (theory == TheoryCatalog.Chemtrails)
      return new ChemtrailsCard();
    if (theory == TheoryCatalog.LeninMushroom)
      return new LeninMushroomCard();
    if (theory == TheoryCatalog.AliensAmongUs)
      return new AliensAmongUsCard();
    if (theory == TheoryCatalog.AlienTechnology)
      return new AlienTechnologyCard();
    if (theory == TheoryCatalog.AlienExperiments)
      return new AlienExperimentsCard();
    if (theory == TheoryCatalog.AlienEnslavement)
      return new AlienEnslavementCard();
    if (theory == TheoryCatalog.SecretGovernment)
      return new SecretGovernmentCard();
    if (theory == TheoryCatalog.Freemasons)
      return new FreemasonsCard();
    if (theory == TheoryCatalog.Reptilians)
      return new ReptiliansCard();
    if (theory == TheoryCatalog.SatanistsKidnapChildren)
      return new SatanistsCard();
    if (theory == TheoryCatalog.BirdsAreDrones)
      return new BirdsAreDronesCard();
    if (theory == TheoryCatalog.TinFoilHat)
      return new TinFoilHatCard();
    if (theory == TheoryCatalog.PresidentDoubles)
      return new PresidentDoublesCard();
    if (theory == TheoryCatalog.ScienceConspiracy)
      return new ScienceConspiracyCard();
    if (theory == TheoryCatalog.FlatEarth)
      return new FlatEarthCard();
    if (theory == TheoryCatalog.AntiVaccination)
      return new AntiVaccinationCard();
    if (theory == TheoryCatalog.Microchipping)
      return new MicrochippingCard();
    if (theory == TheoryCatalog.FiveGTowers)
      return new FiveGTowersCard();
    if (theory == TheoryCatalog.CiaKillsPeople)
      return new CiaKillsPeopleCard();
    if (theory == TheoryCatalog.DullesPlan)
      return new DullesPlanCard();
    if (theory == TheoryCatalog.CiaEbola)
      return new CiaEbolaCard();
    if (theory == TheoryCatalog.CommunistConspiracy)
      return new CommunistConspiracyCard();
    if (theory == TheoryCatalog.CommunistsOnMoon)
      return new CommunistsOnMoonCard();
    if (theory == TheoryCatalog.CommunistsInAntarctica)
      return new CommunistsInAntarcticaCard();
    if (theory == TheoryCatalog.Illuminati)
      return new IlluminatiCard();
    return null;
  }
}
