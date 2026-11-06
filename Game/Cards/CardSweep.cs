using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Подсветка карточки: белый свет проходит по ней сверху вниз и гаснет.
/// </summary>
public static class CardSweep
{
  /// <summary>
  /// Сколько секунд свет проходит по карточке.
  /// </summary>
  public const double Duration = 0.8;

  private const string Code = """
    shader_type canvas_item;

    uniform float progress : hint_range(0.0, 1.0) = 0.0;
    uniform float half_height = 90.0;

    varying vec2 local;

    void vertex() {
      local = VERTEX;
    }

    void fragment() {
      // 0 — верх карточки, 1 — низ.
      float t = clamp((local.y + half_height) / (2.0 * half_height), 0.0, 1.0);
      // Полоса света идёт сверху вниз; за пределами карточки её уже нет.
      float front = progress * 1.3 - 0.15;
      float band = 1.0 - smoothstep(0.0, 0.14, abs(t - front));
      COLOR.rgb = mix(COLOR.rgb, vec3(1.0), band * 0.9);
    }
    """;

  /// <summary>
  /// Запустить подсветку карточки. После анимации карточка возвращается к обычному виду.
  /// </summary>
  /// <param name="view">Карточка теории на столе.</param>
  public static void Play(CardView view)
  {
    var material = new ShaderMaterial { Shader = new Shader { Code = Code } };
    material.SetShaderParameter("half_height", CardPainter.CardSize.Y / 2f);
    material.SetShaderParameter("progress", 0f);
    view.Material = material;

    var tween = view.CreateTween();
    tween.TweenMethod(Callable.From<float>(value => material.SetShaderParameter("progress", value)), 0f, 1f, Duration);
    tween.TweenCallback(Callable.From(() =>
    {
      if (GodotObject.IsInstanceValid(view) && view.Material == material)
        view.Material = null;
    }));
  }
}
