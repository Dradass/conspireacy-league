using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Table;
using Godot;

namespace ConspiracyLeague.Game.Samples;

/// <summary>
/// Записывает витрину <see cref="AdeptWalkSamples"/> в PNG-кадры с двойным разрешением (для сглаживания при уменьшении).
/// Запуск: <c>godot --path . --fixed-fps 25 res://Game/Samples/SampleCapture.tscn -- --out=папка --frames=75</c>;
/// с <c>--cards</c> — витрину <see cref="AdeptsCardSample"/>; с <c>--hq</c> — витрину <see cref="AdeptsHqSample"/>
/// (разбегание от кликов); с <c>--game</c> — партию ботов: она идёт ускоренно, пока у какой-нибудь теории
/// не наберётся адептов, потом камера смотрит на этот штаб и кадры пишутся в обычном темпе
/// (с <c>--scare</c> — ещё и с кликами по штабу).
/// </summary>
public partial class SampleCapture : Node
{
  private const int Supersample = 2;

  private SubViewport _viewport = null!;
  private string _out = "user://capture";
  private int _frames = 75;
  private int _frame = -2;
  private bool _cards;
  private bool _game;
  private bool _scare;
  private bool _hq;
  private int _warmup;
  private Board? _board;
  private TableCamera? _camera;

  public override void _Ready()
  {
    foreach (var arg in OS.GetCmdlineUserArgs())
    {
      if (arg.StartsWith("--out="))
        _out = arg["--out=".Length..];
      else if (arg == "--cards")
        _cards = true;
      else if (arg == "--game")
        _game = true;
      else if (arg == "--scare")
        _scare = true;
      else if (arg == "--hq")
        _hq = true;
      else if (arg.StartsWith("--frames="))
        _frames = int.Parse(arg["--frames=".Length..]);
    }

    DirAccess.MakeDirRecursiveAbsolute(_out);
    _viewport = new SubViewport
    {
      Size = (Vector2I)(AdeptWalkSamples.StageSize * Supersample),
      RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
    };
    AddChild(_viewport);
    if (_game)
    {
      _viewport.Size = new Vector2I(1920, 1080);
      _viewport.AddChild(new Main
      {
        Name = "Main", PlayerCount = 4, BotSeats = [true, true, true, true], BotLevels = Enumerable.Repeat(BotLevel.Easy, 4).ToList(),
        BotSlowdown = 0.05,
      });
      Engine.TimeScale = 8;
      return;
    }

    if (_hq)
    {
      _viewport.AddChild(new AdeptsHqSample { Scale = Vector2.One * Supersample });
      return;
    }

    _viewport.AddChild(_cards
      ? new AdeptsCardSample { Scale = Vector2.One * Supersample }
      : new AdeptWalkSamples { Scale = Vector2.One * Supersample });
  }

  public override void _Process(double delta)
  {
    if (_game && !WatchGame())
      return;

    // Первые кадры пропускаются: картинка в текстуре появляется с задержкой на кадр.
    if (++_frame < 0)
      return;
    if (_frame >= _frames)
    {
      GetTree().Quit();
      return;
    }

    _viewport.GetTexture().GetImage().SavePng($"{_out}/f{_frame:D4}.png");
  }

  /// <summary>
  /// Партия ботов: ждать, пока у теории не станет хотя бы 8 адептов, потом держать камеру на её штабе.
  /// </summary>
  /// <returns>Пора писать кадры.</returns>
  private bool WatchGame()
  {
    _board ??= FindNode<Board>(_viewport);
    _camera ??= FindNode<TableCamera>(_viewport);
    if (_board is null || _camera is null)
      return false;

    var column = _board.Field.TheoryColumns.Where(column => column.IsHeadquarters).MaxBy(column => column.Adepts);
    if (Engine.TimeScale > 1)
    {
      if ((column is null || column.Adepts < 8) && ++_warmup < 9000)
      {
        _frame = -2;
        return false;
      }

      Engine.TimeScale = 1;
      GD.Print($"Снимаю штаб «{column?.Theory.Name}»: адептов {column?.Adepts}, кадров разгона {_warmup}");
    }

    // Интерфейс (рука, история, окна итогов) закрывал бы стол.
    foreach (var layer in _viewport.FindChildren("*", nameof(CanvasLayer), owned: false).OfType<CanvasLayer>())
      layer.Visible = false;

    if (column is null)
      return true;

    if (_scare)
      ClickAt(column);

    var cells = column.Cells.Select(cell => _board.CellCenter(cell.ToVector2I())).ToList();
    _camera.GlobalPosition = _board.ToGlobal(cells.Aggregate((a, b) => a + b) / cells.Count);
    _camera.Zoom = Vector2.One * (_scare ? 2f : 1.15f);
    return true;
  }

  /// <summary>
  /// Клики по штабу: один, второй через 2,4 с, потом пять подряд — адепты разбегаются всё быстрее.
  /// </summary>
  private void ClickAt(Core.Field.TheoryColumn column)
  {
    int[] clicks = [25, 85, 150, 158, 166, 174, 182];
    if (!clicks.Contains(_frame) || _board?.FindChild($"Adepts{column.Theory.Name}", true, false) is not AdeptsView adepts)
      return;

    var meeple = adepts.GetChildren().OfType<WalkingAdeptView>().FirstOrDefault();
    if (meeple is not null)
      adepts.Scare(meeple.Position + new Vector2(6, 2));
  }

  private static T? FindNode<T>(Node root) where T : Node
  {
    foreach (var child in root.GetChildren())
    {
      if (child is T found)
        return found;
      if (FindNode<T>(child) is { } nested)
        return nested;
    }

    return null;
  }
}
