namespace Application.Abstractions;

public interface IAppLifetime
{
    CancellationToken Stopping { get; }
}
