namespace AgentCodeGen.Api.Functional;

public readonly record struct Either<TLeft, TRight>
{
    private readonly TLeft _left;
    private readonly TRight _right;
    private readonly bool _isRight;

    private Either(TLeft left, TRight right, bool isRight)
    {
        _left = left;
        _right = right;
        _isRight = isRight;
    }

    public static Either<TLeft, TRight> Left(TLeft left) => new(left, default!, isRight: false);

    public static Either<TLeft, TRight> Right(TRight right) => new(default!, right, isRight: true);

    public bool IsRight => _isRight;

    public TResult Match<TResult>(Func<TLeft, TResult> left, Func<TRight, TResult> right) =>
        _isRight ? right(_right) : left(_left);

    public Either<TLeft, TResult> Map<TResult>(Func<TRight, TResult> map) =>
        _isRight ? Either<TLeft, TResult>.Right(map(_right)) : Either<TLeft, TResult>.Left(_left);
}
