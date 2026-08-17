namespace AgentCodeGen.Api.Functional;

public readonly record struct Option<T>
{
    private readonly T _value;
    private readonly bool _isSome;

    private Option(T value)
    {
        _value = value;
        _isSome = true;
    }

    public static Option<T> Some(T value) => new(value);

    public static Option<T> None => default;

    public bool IsSome => _isSome;

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) =>
        _isSome ? some(_value) : none();

    public Option<TResult> Map<TResult>(Func<T, TResult> map) =>
        _isSome ? Option<TResult>.Some(map(_value)) : Option<TResult>.None;
}
