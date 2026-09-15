namespace Mochi.Native;

internal static class PetTiming
{
    public const int IdleTickMs = 250;
    public const int WalkTickMs = 33;
    public const int BlinkTickMs = 40;
    public const int WalkFrameMs = 110;
    public const int GroomFrameMs = 220;
    public const int BlinkDurationMs = 240;
    public const int BlinkHalfPhaseMs = 60;

    public static long NowMs => Environment.TickCount64;
    public static int WalkDelay(Random random) => random.Next(40_000, 80_001);
    public static int MicroDelay(Random random) => random.Next(18_000, 35_001);
    public static int BlinkDelay(Random random) => random.Next(7_000, 12_001);
    public static int InitialBlinkDelay(Random random) => random.Next(4_000, 9_001);
    public static int ActionDurationMs(int action) => action switch { 1 => 3_000, 2 => 7_000, 3 => 5_000, _ => 0 };

    public static long PostponeIfDue(long deadline, long now, int delay) => deadline <= now ? now + delay : deadline;
}
