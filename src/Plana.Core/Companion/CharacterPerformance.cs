namespace Plana.Core.Companion;

public enum CharacterEmotion
{
    Neutral,
    Happy,
    Excited,
    Surprised,
    Sad,
    Worried,
    Angry,
    Affectionate,
    Shy,
    Dizzy
}

public enum CharacterGesture
{
    None,
    Blink,
    HeadPat,
    LookAtPointer
}

public sealed record CharacterPerformanceIntent(
    CharacterEmotion Emotion = CharacterEmotion.Neutral,
    CharacterGesture Gesture = CharacterGesture.None,
    bool IsSpeaking = false);

public sealed record SpineAnimationCue(string Animation, bool Loop = false);

public sealed record CharacterPerformancePlan(IReadOnlyList<SpineAnimationCue> Cues);

/// <summary>
/// Maps stable character meaning used by AI and interactions to the current Plana Spine rig.
/// Callers do not need to know numbered animation or sequencing conventions.
/// </summary>
public sealed class PlanaPerformancePlanner
{
    private static readonly IReadOnlyDictionary<CharacterEmotion, string> ExpressionAnimations =
        new Dictionary<CharacterEmotion, string>
        {
            [CharacterEmotion.Neutral] = "00",
            [CharacterEmotion.Happy] = "16",
            [CharacterEmotion.Excited] = "09",
            [CharacterEmotion.Surprised] = "07",
            [CharacterEmotion.Sad] = "20",
            [CharacterEmotion.Worried] = "12",
            [CharacterEmotion.Angry] = "10",
            [CharacterEmotion.Affectionate] = "17",
            [CharacterEmotion.Shy] = "18",
            [CharacterEmotion.Dizzy] = "13"
        };

    public CharacterPerformancePlan Plan(CharacterPerformanceIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var cues = new List<SpineAnimationCue>();
        switch (intent.Gesture)
        {
            case CharacterGesture.Blink:
                cues.Add(new SpineAnimationCue("Eye_Close_01"));
                break;
            case CharacterGesture.HeadPat:
                cues.Add(new SpineAnimationCue("S_Pat_01_M_all"));
                break;
            case CharacterGesture.LookAtPointer:
                cues.Add(new SpineAnimationCue("S_Look_01_all"));
                break;
        }

        var expression = ExpressionAnimations[intent.Emotion];
        if (intent.IsSpeaking && intent.Emotion == CharacterEmotion.Neutral)
            expression = "03";

        cues.Add(new SpineAnimationCue(expression));
        cues.Add(new SpineAnimationCue("Idle_01", Loop: true));
        return new CharacterPerformancePlan(cues);
    }
}

public sealed class PlanaInteractionPlanner(Random? random = null)
{
    private static readonly CharacterEmotion[] Emotions =
    [
        CharacterEmotion.Happy, CharacterEmotion.Excited, CharacterEmotion.Surprised,
        CharacterEmotion.Shy, CharacterEmotion.Affectionate, CharacterEmotion.Dizzy,
        CharacterEmotion.Worried, CharacterEmotion.Angry
    ];

    private static readonly CharacterGesture[] Gestures =
    [
        CharacterGesture.None, CharacterGesture.Blink,
        CharacterGesture.LookAtPointer, CharacterGesture.HeadPat
    ];

    private readonly Random _random = random ?? Random.Shared;

    public CharacterPerformanceIntent PlanRandomInteraction() => new(
        Emotions[_random.Next(Emotions.Length)],
        Gestures[_random.Next(Gestures.Length)]);
}
