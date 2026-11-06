using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Итог проверки штабов теорий в конце раунда: какие штабы закрыли и какие открыли обратно.
/// </summary>
/// <param name="Closed">Штабы, закрытые как несостоятельные.</param>
/// <param name="Reopened">Закрытые штабы, которые снова стали состоятельными и открыты.</param>
public sealed record SolvencyChanges(IReadOnlyList<TheoryColumn> Closed, IReadOnlyList<TheoryColumn> Reopened);
