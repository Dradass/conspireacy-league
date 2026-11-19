using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Tests;

public sealed class SatanistsPassiveTests
{
  /// <summary>
  /// Бездомные приносят больше только владельцу карточки «Сатанисты похищают детей», а не всем игрокам.
  /// </summary>
  [Test]
  public void HomelessBonus_OnlyForTheoryCardOwner()
  {
    var field = new PlayingField(20, 20, new Zone(0, 5, 3, 5));
    var owner = new Player("А", PlayerColor.Red, 0);
    var rival = new Player("Б", PlayerColor.Blue, 0);
    field.AddPlayer(owner, new Zone(0, 12, 7, 4));
    field.AddPlayer(rival, new Zone(8, 12, 7, 4));

    // Действующая теория с тремя лидерами — владелец получает её карточку.
    for (var row = 9; row >= 7; row--)
      field.Place(new CitizenCard(TheoryCatalog.SatanistsKidnapChildren), new Cell(0, row), owner);
    Assert.That(field.TheoryCardOf(TheoryCatalog.SatanistsKidnapChildren)?.Owner, Is.EqualTo(owner));

    var ownerHomeless = new Cell(0, 12);
    var rivalHomeless = new Cell(8, 12);
    field.Place(new HomelessCard(), ownerHomeless, owner);
    field.Place(new HomelessCard(), rivalHomeless, rival);

    void Use(Cell cell, Player player) =>
      field.Use(cell, field.CardAt(cell)!.OnPlayFromHand.Options.Single(), new GameContext(player, field));

    // Владелец уже получил деньги за выкладывание лидеров — считаем только розыгрыш бездомного.
    var start = owner.Money;
    Use(ownerHomeless, owner);
    Use(rivalHomeless, rival);

    Assert.That(owner.Money - start, Is.EqualTo(2 + SatanistsPassive.HomelessBonus), "Владелец карточки получает бонус.");
    Assert.That(rival.Money, Is.EqualTo(2), "Другой игрок бонуса не получает.");
  }
}
