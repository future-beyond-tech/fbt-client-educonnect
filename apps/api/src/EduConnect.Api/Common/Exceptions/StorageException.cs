namespace EduConnect.Api.Common.Exceptions;

/// <summary>
/// Thrown when the underlying object-store backend (S3, R2, MinIO) fails
/// in a way the API cannot recover from inline. Distinct from generic
/// 500 errors so the global exception middleware can map it to
/// <c>502 Bad Gateway</c>: the failure is upstream, not a server-side
/// bug.
/// </summary>
public class StorageException : Exception
{
    public StorageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
