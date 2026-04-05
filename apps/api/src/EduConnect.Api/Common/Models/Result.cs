namespace EduConnect.Api.Common.Models;

public abstract record Result
{
    public sealed record Success(object? Value = null) : Result;
    public sealed record Failure(string ErrorMessage) : Result;

    public static Result SuccessResult(object? value = null) => new Success(value);
    public static Result FailureResult(string errorMessage) => new Failure(errorMessage);
}

public abstract record Result<T>
{
    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(string ErrorMessage) : Result<T>;

    public static Result<T> SuccessResult(T value) => new Success(value);
    public static Result<T> FailureResult(string errorMessage) => new Failure(errorMessage);
}
