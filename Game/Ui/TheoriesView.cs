using ConspiracyLeague.Game.Table;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно «Теории»: все выложенные штабы теорий так, как они лежат на столе, — стопки лидеров, дочерние теории,
/// миплы, адепты и итоги под теориями. Это живая картинка стола: отдельная камера в <see cref="SubViewport"/>
/// смотрит в тот же мир, что и основная, и кадрирует общую зону (<see cref="Board.TheoriesRect"/>), поэтому окно
/// следит за ростом зоны само. Правая кнопка — описание карточки, двойной щелчок — перейти к теориям на столе.
/// </summary>
public partial class TheoriesView : Control
{
  /// <summary>
  /// Натуральный размер окна относительно стола: по нему окно подбирает начальный размер.
  /// </summary>
  private const float PreviewScale = 0.32f;

  private SubViewport _viewport = null!;
  private Camera2D _camera = null!;
  private TextureRect _picture = null!;
  private Rect2 _area;

  /// <summary>
  /// Стол: из его мира берётся картинка, по нему считается область теорий.
  /// </summary>
  public Board Board { get; init; } = null!;

  /// <summary>
  /// Связь окон «Теории» с контроллерами.
  /// </summary>
  public TheoriesHub Hub { get; init; } = null!;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Stop;
    TooltipText = "Правая кнопка — описание карточки. Двойной щелчок — перейти к теориям на столе.";

    _viewport = new SubViewport
    {
      Name = "Viewport",
      // Тот же мир, что у основного окна: на картинке ровно то, что лежит на столе. Слои интерфейса (CanvasLayer)
      // принадлежат основному окну и сюда не попадают.
      World2D = Board.GetViewport().World2D,
      Disable3D = true,
      HandleInputLocally = false,
      GuiDisableInput = true,
      RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
    };
    AddChild(_viewport);

    _camera = new Camera2D { Name = "Camera" };
    _viewport.AddChild(_camera);
    _camera.MakeCurrent();

    _picture = new TextureRect
    {
      Name = "Picture",
      Texture = _viewport.GetTexture(),
      ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
      StretchMode = TextureRect.StretchModeEnum.Scale,
      MouseFilter = MouseFilterEnum.Ignore,
    };
    _picture.SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(_picture);

    UpdateArea();
  }

  public override void _Process(double delta)
  {
    // Окно из раскладки другого игрока не видно — не тратим на него отрисовку.
    var visible = IsVisibleInTree();
    _viewport.RenderTargetUpdateMode = visible ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    if (!visible)
      return;

    UpdateArea();
    // Картинку рендерим в разрешении экрана: окно масштабирует содержимое, и иначе она была бы размытой.
    var pixels = (Size * GetGlobalTransform().Scale).Round();
    var size = new Vector2I(Math.Max(1, (int)pixels.X), Math.Max(1, (int)pixels.Y));
    if (_viewport.Size != size)
      _viewport.Size = size;

    // Вписываем область теорий в картинку целиком, по центру.
    _camera.Position = _area.GetCenter();
    var zoom = Mathf.Min(size.X / Mathf.Max(_area.Size.X, 1), size.Y / Mathf.Max(_area.Size.Y, 1));
    _camera.Zoom = Vector2.One * zoom;
  }

  public override void _GuiInput(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true }:
        AcceptEvent();
        Hub.RaiseFocus(_area.GetCenter());
        break;
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } press:
        AcceptEvent();
        Hub.RaiseInfoPressed(TablePointAt(press.Position));
        break;
      case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right }:
        AcceptEvent();
        Hub.RaiseInfoReleased();
        break;
    }
  }

  /// <summary>
  /// Точка стола под точкой окна.
  /// </summary>
  private Vector2 TablePointAt(Vector2 local)
  {
    var pixels = new Vector2(_viewport.Size.X, _viewport.Size.Y);
    var onPicture = local * pixels / new Vector2(Mathf.Max(Size.X, 1), Mathf.Max(Size.Y, 1));
    return _viewport.CanvasTransform.AffineInverse() * onPicture;
  }

  /// <summary>
  /// Область теорий растёт вместе с общей зоной — вместе с ней меняется и натуральный размер окна.
  /// </summary>
  private void UpdateArea()
  {
    var area = Board.TheoriesRect();
    if (area == _area)
      return;
    _area = area;
    CustomMinimumSize = (area.Size * PreviewScale).Round();
  }
}
