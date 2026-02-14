namespace HotsPatchNotes.Web.Services;

/// <summary>
/// Severity level for error messages.
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Represents an error to display to the user.
/// </summary>
public record ErrorInfo(string Message, string? Detail, ErrorSeverity Severity, DateTime Timestamp);

/// <summary>
/// Service for managing global error state in the application.
/// </summary>
public interface IErrorStateService
{
    /// <summary>
    /// Sets the current error to display.
    /// </summary>
    void SetError(string message, string? detail = null, ErrorSeverity severity = ErrorSeverity.Error);

    /// <summary>
    /// Clears the current error.
    /// </summary>
    void ClearError();

    /// <summary>
    /// Event fired when the error state changes.
    /// </summary>
    event Action<ErrorInfo?>? OnErrorChanged;

    /// <summary>
    /// Gets the current error, if any.
    /// </summary>
    ErrorInfo? CurrentError { get; }
}

/// <summary>
/// Singleton service for managing application error state with event-based notifications.
/// </summary>
public sealed class ErrorStateService : IErrorStateService
{
    private ErrorInfo? _currentError;
    private Timer? _autoDismissTimer;
    private readonly object _lock = new();

    /// <inheritdoc />
    public event Action<ErrorInfo?>? OnErrorChanged;

    /// <inheritdoc />
    public ErrorInfo? CurrentError
    {
        get
        {
            lock (_lock)
            {
                return _currentError;
            }
        }
    }

    /// <inheritdoc />
    public void SetError(string message, string? detail = null, ErrorSeverity severity = ErrorSeverity.Error)
    {
        lock (_lock)
        {
            // Cancel any existing auto-dismiss timer
            _autoDismissTimer?.Dispose();
            _autoDismissTimer = null;

            // Set new error
            _currentError = new ErrorInfo(message, detail, severity, DateTime.Now);

            // Auto-dismiss after 10 seconds for Info and Warning severity
            if (severity == ErrorSeverity.Info || severity == ErrorSeverity.Warning)
            {
                _autoDismissTimer = new Timer(_ => ClearError(), null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
            }
        }

        // Notify subscribers outside the lock to avoid deadlocks
        OnErrorChanged?.Invoke(_currentError);
    }

    /// <inheritdoc />
    public void ClearError()
    {
        lock (_lock)
        {
            // Cancel auto-dismiss timer if active
            _autoDismissTimer?.Dispose();
            _autoDismissTimer = null;

            _currentError = null;
        }

        // Notify subscribers outside the lock
        OnErrorChanged?.Invoke(null);
    }
}
