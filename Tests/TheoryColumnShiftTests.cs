using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Tests;

public sealed class TheoryColumnShiftTests
{
  /// <summary>
  /// Базовая теория, выложенная под дочернюю, сдвигает теории в сторону; если сдвиг упирается в край общей зоны,
  /// а поле дальше свободно, зона расширяется, а не запрещает выкладывание.
  /// </summary>
  [Test]
  public void BaseTheoryUnderChild_ShiftsBeyondCommonZoneEdge()
  {
    // Общая зона: столбцы 0..2, ряды 5..9 (нижний ряд 9, ряд дочерних теорий 7).
    var field = new PlayingField(20, 20, new Zone(0, 5, 3, 5));
    var player = new Player("А", PlayerColor.Red, 1000);
    field.AddPlayer(player, new Zone(0, 12, 7, 4));

    void Place(Theory theory, int column, int row) =>
      field.Place(new CitizenCard(theory), new Cell(column, row), player);

    // Родительская теория в три карточки и две дочерние справа от её третьей карточки.
    Place(TheoryCatalog.AliensAmongUs, 0, 9);
    Place(TheoryCatalog.AliensAmongUs, 0, 8);
    Place(TheoryCatalog.AliensAmongUs, 0, 7);
    Place(TheoryCatalog.AlienTechnology, 1, 7);
    Place(TheoryCatalog.AlienExperiments, 2, 7);

    // Каждая новая базовая теория под первой дочерней сдвигает теории справа на столбец.
    Place(TheoryCatalog.SecretGovernment, 1, 9);
    Place(TheoryCatalog.ScienceConspiracy, 1, 9);
    var zoneRight = field.CommonZone.Left + field.CommonZone.Width - 1;
    Assert.That(field.TheoryColumns.Max(column => column.TheoryCell.Column), Is.EqualTo(zoneRight),
      "Теории дошли до края общей зоны.");

    var check = field.Check(new CitizenCard(TheoryCatalog.GoldenBillion), new Cell(1, 9), player);
    Assert.That(check.IsAllowed, Is.True, check.Reason);
    Place(TheoryCatalog.GoldenBillion, 1, 9);

    Assert.That(field.CommonZone.Left + field.CommonZone.Width - 1, Is.EqualTo(zoneRight + 1));
    Assert.That(field.TheoryColumns.Select(column => column.TheoryCell.Column), Is.Unique);
    Assert.That(field.TheoryColumns.All(column => column.Cells.All(field.CommonZone.Contains)), Is.True);
  }
}
