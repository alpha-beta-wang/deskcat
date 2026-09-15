using Mochi.Native;

var first = new Random(20260915);
var second = new Random(20260915);
for (var i = 0; i < 10_000; i++)
{
    var a = (PetTiming.WalkDelay(first), PetTiming.MicroDelay(first), PetTiming.BlinkDelay(first));
    var b = (PetTiming.WalkDelay(second), PetTiming.MicroDelay(second), PetTiming.BlinkDelay(second));
    if (a != b) throw new InvalidOperationException("pet schedules diverged for the same random seed");
    if (a.Item1 is < 40_000 or > 80_000) throw new InvalidOperationException("walk delay out of range");
    if (a.Item2 is < 18_000 or > 35_000) throw new InvalidOperationException("micro-action delay out of range");
    if (a.Item3 is < 7_000 or > 12_000) throw new InvalidOperationException("blink delay out of range");
}

const long now = 100_000;
if (PetTiming.PostponeIfDue(99_999, now, 40_000) != 140_000)
    throw new InvalidOperationException("overdue walk was not postponed by a full interval");
if (PetTiming.PostponeIfDue(140_000, now, 40_000) != 140_000)
    throw new InvalidOperationException("future deadline changed unexpectedly");

Console.WriteLine("Timing self-test passed: 10,000 identical schedules; walk 40-80s, action 18-35s, blink 7-12s.");

static List<(string Kind, long At)> SimulateTenMinutes(int seed)
{
    var random = new Random(seed);
    var events = new List<(string, long)>();
    long now = 0;
    long nextWalk = PetTiming.WalkDelay(random);
    long nextAction = PetTiming.MicroDelay(random);
    long nextBlink = PetTiming.BlinkDelay(random);
    while (now < 600_000)
    {
        var next = Math.Min(nextWalk, Math.Min(nextAction, nextBlink));
        now = next;
        if (now >= 600_000) break;
        if (next == nextWalk)
        {
            events.Add(("walk", now));
            now += 1_400;
            nextWalk = now + PetTiming.WalkDelay(random);
            nextAction = PetTiming.PostponeIfDue(nextAction, now, PetTiming.MicroDelay(random));
            nextBlink = PetTiming.PostponeIfDue(nextBlink, now, PetTiming.BlinkDelay(random));
        }
        else if (next == nextAction)
        {
            var action = random.Next(1, 4);
            events.Add(($"action-{action}", now));
            now += PetTiming.ActionDurationMs(action);
            nextAction = now + PetTiming.MicroDelay(random);
            nextWalk = PetTiming.PostponeIfDue(nextWalk, now, PetTiming.WalkDelay(random));
            nextBlink = PetTiming.PostponeIfDue(nextBlink, now, PetTiming.BlinkDelay(random));
        }
        else
        {
            events.Add(("blink", now));
            nextBlink = now + PetTiming.BlinkDelay(random);
            now += PetTiming.BlinkDurationMs;
        }
    }
    return events;
}

var mochi = SimulateTenMinutes(42);
var niangao = SimulateTenMinutes(42);
if (!mochi.SequenceEqual(niangao))
    throw new InvalidOperationException("ten-minute pet event logs diverged");
var walks = mochi.Where(item => item.Kind == "walk").Select(item => item.At).ToArray();
if (walks.Zip(walks.Skip(1), (left, right) => right - left).Any(gap => gap < 40_000))
    throw new InvalidOperationException("walk starts were less than 40 seconds apart");
Console.WriteLine($"Accelerated 10-minute simulation passed: {walks.Length} walks, {mochi.Count(item => item.Kind.StartsWith("action"))} actions, {mochi.Count(item => item.Kind == "blink")} blinks; Mochi and Niangao logs identical.");
