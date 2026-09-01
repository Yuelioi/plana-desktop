using Plana.Core.Companion;

namespace Plana.Core.Tests;

public sealed class PlanaPerformancePlannerTests
{
    private readonly PlanaPerformancePlanner planner = new();

    [Theory]
    [InlineData(CharacterEmotion.Neutral, "00")]
    [InlineData(CharacterEmotion.Happy, "16")]
    [InlineData(CharacterEmotion.Excited, "09")]
    [InlineData(CharacterEmotion.Surprised, "07")]
    [InlineData(CharacterEmotion.Sad, "20")]
    [InlineData(CharacterEmotion.Worried, "12")]
    [InlineData(CharacterEmotion.Angry, "10")]
    [InlineData(CharacterEmotion.Affectionate, "17")]
    [InlineData(CharacterEmotion.Shy, "18")]
    [InlineData(CharacterEmotion.Dizzy, "13")]
    public void MapsSemanticEmotionWithoutExposingNumberedAnimationsToCallers(CharacterEmotion emotion, string animation)
    {
        var plan = planner.Plan(new CharacterPerformanceIntent(emotion));

        Assert.Equal(animation, plan.Cues[0].Animation);
        Assert.Equal(new SpineAnimationCue("Idle_01", Loop: true), plan.Cues[^1]);
    }

    [Theory]
    [InlineData(CharacterGesture.Blink, "Eye_Close_01")]
    [InlineData(CharacterGesture.HeadPat, "S_Pat_01_M_all")]
    [InlineData(CharacterGesture.LookAtPointer, "S_Look_01_all")]
    public void SequencesGestureBeforeExpressionAndIdle(CharacterGesture gesture, string animation)
    {
        var plan = planner.Plan(new CharacterPerformanceIntent(CharacterEmotion.Happy, gesture));

        Assert.Equal([animation, "16", "Idle_01"], plan.Cues.Select(cue => cue.Animation));
    }

    [Fact]
    public void UsesNeutralSpeechPoseWithoutChangingTheAiFacingEmotionContract()
    {
        var plan = planner.Plan(new CharacterPerformanceIntent(IsSpeaking: true));

        Assert.Equal("03", plan.Cues[0].Animation);
    }

    [Fact]
    public void RandomInteractionsDoNotCollapseToOneHardCodedPerformance()
    {
        var interactions = new PlanaInteractionPlanner(new Random(12345));

        var results = Enumerable.Range(0, 12).Select(_ => interactions.PlanRandomInteraction()).ToHashSet();

        Assert.True(results.Count >= 4);
        Assert.All(results, intent => Assert.NotEqual(CharacterEmotion.Neutral, intent.Emotion));
    }
}
