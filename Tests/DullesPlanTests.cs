using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Tests;

public sealed class DullesPlanTests
{
  /// <summary>
  /// «План Даллеса» понижает персонажей только в личных штабах игроков, а не лидеров в штабах теорий.
  /// </summary>
  [Test]
  public void Demote_OnlyPersonalHq()
  {
    var field = new PlayingField(20, 20, new Zone(0, 5, 3, 5));
    var player = new Player("А", PlayerColor.Red, 0);
    field.AddPlayer(player, new Zone(0, 12, 7, 4));

    var leader = new Cell(0, 9);
    field.Place(new CitizenCard(TheoryCatalog.SatanistsKidnapChildren), leader, player);
    var personal = new Cell(0, 12);
    field.Place(new CitizenCard(TheoryCatalog.SatanistsKidnapChildren), personal, player);

    Assert.That(field.DemoteTargets(player).Select(target => target.Cell), Is.EqualTo(new[] { personal }));
    Assert.That(field.CheckDemote(leader, player).IsAllowed, Is.False, "Лидера в штабе теории не понизить.");
    Assert.That(field.CheckDemote(personal, player).IsAllowed, Is.True);

    field.Demote(personal, player);
    Assert.That(field.CardAt(personal), Is.InstanceOf<HomelessCard>());
    Assert.That(field.CardAt(leader), Is.InstanceOf<CitizenCard>());
  }
}
