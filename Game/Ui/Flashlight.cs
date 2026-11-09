using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// «Фонарик» в меню: курсор подсвечивает вокруг себя круг. Яркое пятно — примерно 1/20 ширины экрана в поперечнике,
/// дальше свет мягко гаснет. Лежит поверх всего меню и не перехватывает мышь.
/// </summary>
public partial class Flashlight : ColorRect
{
  /// <summary>
  /// Поперечник яркого пятна в долях ширины экрана.
  /// </summary>
  private const float DiameterShare = 1f / 20f;

  private const string ShaderCode = """
    shader_type canvas_item;
    render_mode blend_add;

    uniform vec2 rect_size = vec2(1152.0, 648.0);
    uniform vec2 light_position = vec2(-1000.0);
    uniform float radius = 29.0;
    uniform vec4 light_color : source_color = vec4(0.72, 1.0, 0.78, 1.0);
    uniform float intensity = 0.4;

    void fragment() {
      float dist = distance(UV * rect_size, light_position);
      // Ровное пятно до радиуса, затем свет гаснет ещё на столько же.
      float glow = 1.0 - smoothstep(radius * 0.6, radius * 2.0, dist);
      COLOR = vec4(light_color.rgb, glow * intensity);
    }
    """;

  private ShaderMaterial _material = null!;

  public override void _Ready()
  {
    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    MouseFilter = MouseFilterEnum.Ignore;
    _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
    Material = _material;
  }

  public override void _Process(double delta)
  {
    var mouse = GetLocalMousePosition();
    // Курсор ушёл из окна — фонарик гаснет.
    var inside = new Rect2(Vector2.Zero, Size).HasPoint(mouse);
    _material.SetShaderParameter("rect_size", Size);
    _material.SetShaderParameter("light_position", inside ? mouse : new Vector2(-10000, -10000));
    _material.SetShaderParameter("radius", Size.X * DiameterShare / 2);
  }
}
