namespace ConspiracyLeague.Core.Theories;

/// <summary>
/// Список всех теорий игры.
/// </summary>
/// <remarks>
/// Новая теория добавляется сюда: статическим свойством и в <see cref="All"/>.
/// Дочерняя теория объявляется после родительской.
/// </remarks>
public static class TheoryCatalog
{
  /// <summary>
  /// Ленин — гриб.
  /// </summary>
  public static Theory LeninMushroom { get; } = new("Ленин — гриб");

  /// <summary>
  /// Тайное правительство.
  /// </summary>
  public static Theory SecretGovernment { get; } = new("Тайное правительство");

  /// <summary>
  /// Масоны правят миром.
  /// </summary>
  public static Theory Freemasons { get; } = new("Масоны правят миром", SecretGovernment);

  /// <summary>
  /// Рептилоиды у власти.
  /// </summary>
  public static Theory Reptilians { get; } = new("Рептилоиды у власти", SecretGovernment);

  /// <summary>
  /// Иллюминаты.
  /// </summary>
  public static Theory Illuminati { get; } = new("Иллюминаты", SecretGovernment);

  /// <summary>
  /// Инопланетяне среди нас.
  /// </summary>
  public static Theory AliensAmongUs { get; } = new("Инопланетяне среди нас");

  /// <summary>
  /// Технологии от инопланетян.
  /// </summary>
  public static Theory AlienTechnology { get; } = new("Технологии от инопланетян", AliensAmongUs);

  /// <summary>
  /// Опыты инопланетян над людьми.
  /// </summary>
  public static Theory AlienExperiments { get; } = new("Опыты инопланетян над людьми", AliensAmongUs);

  /// <summary>
  /// Инопланетяне порабощают людей.
  /// </summary>
  public static Theory AlienEnslavement { get; } = new("Инопланетяне порабощают людей", AliensAmongUs);

  /// <summary>
  /// Заговор учёных.
  /// </summary>
  public static Theory ScienceConspiracy { get; } = new("Заговор учёных");

  /// <summary>
  /// Плоская Земля.
  /// </summary>
  public static Theory FlatEarth { get; } = new("Плоская Земля", ScienceConspiracy);

  /// <summary>
  /// Прививки опаснее болезней.
  /// </summary>
  public static Theory AntiVaccination { get; } = new("Прививки опаснее болезней", ScienceConspiracy);

  /// <summary>
  /// Чипирование населения.
  /// </summary>
  public static Theory Microchipping { get; } = new("Чипирование населения", ScienceConspiracy);

  /// <summary>
  /// Вышки 5G.
  /// </summary>
  public static Theory FiveGTowers { get; } = new("Вышки 5G", ScienceConspiracy);

  /// <summary>
  /// Шапочка из фольги.
  /// </summary>
  public static Theory TinFoilHat { get; } = new("Шапочка из фольги");

  /// <summary>
  /// Заговор коммунистов.
  /// </summary>
  public static Theory CommunistConspiracy { get; } = new("Заговор коммунистов");

  /// <summary>
  /// Коммунисты на Луне.
  /// </summary>
  public static Theory CommunistsOnMoon { get; } = new("Коммунисты на Луне", CommunistConspiracy);

  /// <summary>
  /// Коммунисты в Антарктиде.
  /// </summary>
  public static Theory CommunistsInAntarctica { get; } = new("Коммунисты в Антарктиде", CommunistConspiracy);

  /// <summary>
  /// ЦРУ убивает людей.
  /// </summary>
  public static Theory CiaKillsPeople { get; } = new("ЦРУ убивает людей");

  /// <summary>
  /// План Даллеса.
  /// </summary>
  public static Theory DullesPlan { get; } = new("План Даллеса", CiaKillsPeople);

  /// <summary>
  /// Эболу создали в ЦРУ.
  /// </summary>
  public static Theory CiaEbola { get; } = new("Эболу создали в ЦРУ", CiaKillsPeople);

  /// <summary>
  /// Золотой миллиард.
  /// </summary>
  public static Theory GoldenBillion { get; } = new("Золотой миллиард");

  /// <summary>
  /// Ковид придумало правительство.
  /// </summary>
  public static Theory CovidHoax { get; } = new("Ковид придумало правительство", GoldenBillion);

  /// <summary>
  /// Химический след самолётов.
  /// </summary>
  public static Theory Chemtrails { get; } = new("Химический след самолётов", GoldenBillion);

  /// <summary>
  /// Сатанисты похищают детей.
  /// </summary>
  public static Theory SatanistsKidnapChildren { get; } = new("Сатанисты похищают детей");

  /// <summary>
  /// Птицы — это дроны.
  /// </summary>
  public static Theory BirdsAreDrones { get; } = new("Птицы — это дроны");

  /// <summary>
  /// Двойники президентов.
  /// </summary>
  public static Theory PresidentDoubles { get; } = new("Двойники президентов");

  /// <summary>
  /// Все теории.
  /// </summary>
  public static IReadOnlyList<Theory> All { get; } =
  [
    LeninMushroom,
    SecretGovernment,
    Freemasons,
    Reptilians,
    Illuminati,
    AliensAmongUs,
    AlienTechnology,
    AlienExperiments,
    AlienEnslavement,
    ScienceConspiracy,
    FlatEarth,
    AntiVaccination,
    Microchipping,
    FiveGTowers,
    TinFoilHat,
    CommunistConspiracy,
    CommunistsOnMoon,
    CommunistsInAntarctica,
    CiaKillsPeople,
    DullesPlan,
    CiaEbola,
    GoldenBillion,
    CovidHoax,
    Chemtrails,
    SatanistsKidnapChildren,
    BirdsAreDrones,
    PresidentDoubles,
  ];

  /// <summary>
  /// Родительские теории.
  /// </summary>
  public static IEnumerable<Theory> Parents => All.Where(theory => !theory.IsChild);

  /// <summary>
  /// Дочерние теории указанной теории.
  /// </summary>
  /// <param name="parent">Родительская теория.</param>
  public static IEnumerable<Theory> ChildrenOf(Theory parent) => All.Where(theory => theory.Parent == parent);
}
