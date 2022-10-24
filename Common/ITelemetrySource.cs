namespace Common;

public interface ITelemetrySource
{
    Task<CanonicalTelemetry> NextAsync(CancellationToken cancellationToken = default);
}
