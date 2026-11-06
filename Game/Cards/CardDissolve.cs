using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Анимация «рассыпания» карточки: она осыпается сверху вниз — верхняя часть исчезает первой, у фронта
/// распада горят искры, а затем карточка удаляется.
/// </summary>
public static class CardDissolve
{
  /// <summary>
  /// Сколько секунд длится рассыпание.
  /// </summary>
  public const double Duration = 0.9;

  private const string Code = """
    shader_type canvas_item;

    uniform float progress : hint_range(0.0, 1.0) = 0.0;
    uniform float half_height = 90.0;

    varying vec2 local;

    float hash(vec2 p) {
      return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
    }

    void vertex() {
      local = VERTEX;
    }

    void fragment() {
      // 0 — верх карточки, 1 — низ.
      float t = clamp((local.y + half_height) / (2.0 * half_height), 0.0, 1.0);
      // Фронт распада идёт сверху вниз; за пределами карточки он уже полностью её прошёл.
      float front = progress * 1.15 - 0.1;
      float below = t - front;
      float noise = hash(floor(local / 3.0));
      if (below < 0.0) {
        COLOR.a = 0.0;
      } else if (below < 0.06) {
        // Искры на фронте распада.
        COLOR.rgb = mix(COLOR.rgb, vec3(1.0, 0.8, 0.4), 0.85);
        if (noise > 0.45) {
          COLOR.a = 0.0;
        }
      } else if (below < 0.14 && noise > 0.72) {
        // Мелкие кусочки, которые уже отделились от карточки.
        COLOR.a = 0.0;
      }
    }
    """;

  /// <summary>
  /// Запустить рассыпание карточки и удалить её, когда анимация закончится.
  /// </summary>
  /// <param name="view">Карточка на столе; после анимации она удаляется.</param>
  public static void Play(CardView view)
  {
    var shader = new Shader { Code = Code };
    var material = new ShaderMaterial { Shader = shader };
    material.SetShaderParameter("half_height", CardPainter.CardSize.Y / 2f);
    material.SetShaderParameter("progress", 0f);
    view.Material = material;

    var tween = view.CreateTween();
    tween.TweenMethod(Callable.From<float>(value => material.SetShaderParameter("progress", value)), 0f, 1f, Duration)
      .SetTrans(Tween.TransitionType.Linear);
    tween.TweenCallback(Callable.From(view.QueueFree));
  }
}
