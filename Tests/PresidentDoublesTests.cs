using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

public sealed class PresidentDoublesTests
{
  /// <summary>
  /// «Двойники президентов» заменяют политиком и профессионала в своей руке: политик встаёт на его место в руке.
  /// Других персонажей в руке заменить нельзя.
  /// </summary>
  [Test]
  public void ReplacesProfessionalInHand()
  {
    var field = new PlayingField(20, 20, new Zone(0, 5, 3, 5));
    var player = new Player("А", PlayerColor.Red, 10);
    field.AddPlayer(player, new Zone(0, 12, 7, 4));
    var journalist = new GenericProfessionalCard(Profession.Journalist);
    var citizen = new CitizenCard();
    player.Hand.Restore([citizen, journalist]);

    var effect = new ReplaceProfessionalWithPoliticianEffect();
    var context = new GameContext(player, field);
    Assert.That(effect.AcceptsHandCardTarget, Is.True);
    Assert.That(effect.CheckHandCard(citizen, context).IsAllowed, Is.False);
    Assert.That(effect.CheckHandCard(journalist, context).IsAllowed, Is.True);

    effect.Execute(context.WithHandCard(journalist));

    Assert.That(player.Hand.Cards, Has.Count.EqualTo(2));
    Assert.That(player.Hand.Cards[0], Is.SameAs(citizen));
    Assert.That(player.Hand.Cards[1], Is.InstanceOf<GenericProfessionalCard>()
      .With.Property(nameof(GenericProfessionalCard.Profession)).EqualTo(Profession.Politician));
  }
}
