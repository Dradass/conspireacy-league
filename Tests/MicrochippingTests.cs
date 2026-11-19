using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

public sealed class MicrochippingTests
{
  /// <summary>
  /// За фишку «Чипирования» владельцу персонажа платят его влияние в $, а не стоимость.
  /// </summary>
  [Test]
  public void Chip_PaysInfluenceToOwner()
  {
    var field = new PlayingField(20, 20, new Zone(0, 5, 3, 5));
    var chipper = new Player("А", PlayerColor.Red, 10);
    var owner = new Player("Б", PlayerColor.Blue, 0);
    field.AddPlayer(chipper, new Zone(0, 12, 7, 4));
    field.AddPlayer(owner, new Zone(8, 12, 7, 4));

    var citizen = new CitizenCard();
    var cell = new Cell(8, 12);
    field.Place(citizen, cell, owner);
    Assume.That(citizen.Influence, Is.Not.EqualTo(citizen.Cost));
    var ownerStart = owner.Money;

    var check = field.CheckChip(cell, chipper);
    Assert.That(check.Cost, Is.EqualTo(citizen.Influence));

    field.Chip(cell, chipper);
    Assert.That(chipper.Money, Is.EqualTo(10 - citizen.Influence));
    Assert.That(owner.Money - ownerStart, Is.EqualTo(citizen.Influence));
  }
}
