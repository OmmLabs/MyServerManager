using System.ComponentModel.DataAnnotations;
using MyServerManager.Configuration;
using MyServerManager.Models;

namespace MyServerManager.Services;

public sealed class OperationCoordinator(AuditService audit, TimeProvider time, ManagerMode mode, IHttpContextAccessor requests,
    SshConnection ssh, ILogger<OperationCoordinator> logger)
{
    private readonly SemaphoreSlim _operation = new(1);

    public async Task<OperationResult> ExecuteAsync(string username, string target, string? service, AuditAction action,
        Func<Task<OperationResult>> perform)
    {
        var entry = new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), target, service, action, username, null, "Operation requested.");
        var directory = audit.CurrentDirectory;
        if (!_operation.Wait(0))
        {
            const string busy = "Another management operation is in progress. Try again when it finishes.";
            await TryAppendAsync(entry with { Succeeded = false, Message = busy }, directory);
            return OperationResult.Failure(busy);
        }

        try
        {
            if (mode.UsesSsh && requests.HttpContext is { } request && !ssh.IsCurrentSession(request.User))
                return OperationResult.Failure("Sign in again before making changes.");
            if (!await TryAppendAsync(entry, directory))
                return OperationResult.Failure("The audit log is unavailable. The operation was cancelled.");

            OperationResult result;
            try { result = await perform(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ValidationException or System.Text.Json.JsonException)
            {
                logger.LogError("Management action {Action} failed with {ErrorType}.", action, error.GetType().Name);
                result = OperationResult.Failure("The operation could not access valid manager data. Check the application logs.");
            }

            if (!await TryAppendAsync(entry with { Timestamp = time.GetUtcNow(), Succeeded = result.Succeeded, Message = result.Message }, directory))
                return result with { Message = result.Message + " The result could not be written to the audit log." };

            return result;
        }
        finally { _operation.Release(); }
    }

    private async Task<bool> TryAppendAsync(AuditEntry entry, string? directory)
    {
        try
        {
            await audit.AppendAsync(entry, directory);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogError("Could not persist audit event {Id} for {Action}. {ErrorType}.", entry.Id, entry.Action, error.GetType().Name);
            return false;
        }
    }
}
