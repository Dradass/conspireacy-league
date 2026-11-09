using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Фон меню в стиле рубашки карточек: зелёный в центре и почти чёрный по краям, с лёгким «бумажным» зерном.
/// </summary>
public partial class MenuBackdrop : ColorRect
{
  private const string ShaderCode = """
    shader_type canvas_item;

    uniform vec4 center_color : source_color = vec4(0.34, 0.72, 0.44, 1.0);
    uniform vec4 edge_color : source_color = vec4(0.035, 0.055, 0.04, 1.0);
    uniform vec2 rect_size = vec2(1152.0, 648.0);
    uniform float grain = 0.06;

    float hash(vec2 p) {
      return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
    }

    void fragment() {
      // 0 в центре, 1 у края: как на рубашке, края темнеют мягко и почти прямоугольно.
      vec2 d = abs(UV - 0.5) * 2.0;
      float vx = smoothstep(0.45, 1.0, d.x);
      float vy = smoothstep(0.3, 1.0, d.y);
      float v = clamp(vx + vy - vx * vy, 0.0, 1.0);
      vec3 color = mix(center_color.rgb, edge_color.rgb, v);
      // Зерно сильнее там, где светлее, — как типографская бумага на рисунке.
      float n = hash(floor(UV * rect_size / 2.0)) - 0.5;
      color += n * grain * (1.0 - v);
      COLOR = vec4(color, 1.0);
    }
    """;

  public override void _Ready()
  {
    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    MouseFilter = MouseFilterEnum.Ignore;
    Material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
  }

  public override void _Process(double delta) => ((ShaderMaterial)Material).SetShaderParameter("rect_size", Size);
}
