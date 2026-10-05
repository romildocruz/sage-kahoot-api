namespace Sage.Kahoot.Domain.Common;

/// <summary>
/// Sucesso com valor ou falha com <see cref="DomainError"/>. Retorno padrão dos handlers da camada Application.
/// </summary>
public readonly struct Result<T>
{
    private readonly T _value;

    private Result(T value)
    {
        _value = value;
        Error = null;
    }

    private Result(DomainError error)
    {
        _value = default!;
        Error = error;
    }

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException($"Result sem valor: {Error!.Code}. Verifique IsSuccess antes de acessar Value.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(DomainError error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(DomainError error) => new(error);

    public Result<TNext> Map<TNext>(Func<T, TNext> map) =>
        IsSuccess ? Result<TNext>.Success(map(_value)) : Result<TNext>.Failure(Error!);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<DomainError, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value) : onFailure(Error!);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

    public static Result<T> Failure<T>(DomainError error) => Result<T>.Failure(error);
}
