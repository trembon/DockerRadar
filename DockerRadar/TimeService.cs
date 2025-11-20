namespace DockerRadar;

public interface ITimeService
{
    DateTime Now();

    DateTime GetNextCheckTime(int? overrideMinMinutes = null, int? overrideMaxMinutes = null);
}

public class TimeService(IConfiguration configuration) : ITimeService
{
    private static readonly Random _random = new();

    public DateTime GetNextCheckTime(int? overrideMinMinutes = null, int? overrideMaxMinutes = null)
    {
        int minMinutes = overrideMinMinutes ?? configuration.GetValue("UpdateCheck:UpdateRangeFrom", 5);
        int maxMinutes = overrideMaxMinutes ?? configuration.GetValue("UpdateCheck:UpdateRangeTo", 15);
        if (minMinutes > maxMinutes)
            throw new ArgumentException("minMinutes cannot be greater than maxMinutes.");

        var range = minMinutes - maxMinutes;
        var randomOffset = _random.NextDouble() * range;
        var totalMinutes = minMinutes + randomOffset;

        return Now().AddMinutes(totalMinutes);
    }

    public DateTime Now()
    {
        return DateTime.UtcNow;
    }
}
