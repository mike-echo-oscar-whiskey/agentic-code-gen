using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Infrastructure;

public sealed class InMemoryRunStore(TimeProvider clock) : IRunStore
{
    private readonly ConcurrentDictionary<RunId, RunState> _runs = new();

    public AgentRun Create(string goal)
    {
        var state = new RunState(RunId.New(), goal);
        _runs[state.Id] = state;
        return state.Snapshot();
    }

    public Option<AgentRun> Get(RunId id) =>
        _runs.TryGetValue(id, out var state)
            ? Option<AgentRun>.Some(state.Snapshot())
            : Option<AgentRun>.None;

    public void Publish(RunId id, AgentKind agent, AgentEventKind kind, string message) =>
        Require(id).Publish(clock.GetUtcNow(), agent, kind, message);

    public void SetCode(RunId id, CodeArtifact code) => Require(id).SetCode(code);

    public void SetReview(RunId id, ReviewResult review) => Require(id).SetReview(review);

    public void SetGates(RunId id, IReadOnlyList<GateResult> gates) => Require(id).SetGates(gates);

    public void Finish(RunId id, RunStatus status) => Require(id).Finish(status);

    public async IAsyncEnumerable<AgentEvent> SubscribeAsync(
        RunId id,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (replay, live) = Require(id).Subscribe();

        foreach (var agentEvent in replay)
        {
            yield return agentEvent;
        }

        if (live is null)
        {
            yield break;
        }

        await foreach (var agentEvent in live.ReadAllAsync(cancellationToken))
        {
            yield return agentEvent;
        }
    }

    private RunState Require(RunId id) =>
        _runs.TryGetValue(id, out var state)
            ? state
            : throw new InvalidOperationException($"Run {id} does not exist.");

    private sealed class RunState(RunId id, string goal)
    {
        private readonly Lock _gate = new();
        private readonly List<AgentEvent> _events = [];
        private readonly List<Channel<AgentEvent>> _subscribers = [];
        private RunStatus _status = RunStatus.Running;
        private Option<CodeArtifact> _code;
        private Option<ReviewResult> _review;
        private IReadOnlyList<GateResult> _gates = [];

        public RunId Id => id;

        public AgentRun Snapshot()
        {
            lock (_gate)
            {
                return new AgentRun(id, goal, _status, [.. _events], _code, _review, _gates);
            }
        }

        public void Publish(DateTimeOffset at, AgentKind agent, AgentEventKind kind, string message)
        {
            AgentEvent agentEvent;
            Channel<AgentEvent>[] targets;

            lock (_gate)
            {
                agentEvent = new AgentEvent(_events.Count + 1, at, agent, kind, message);
                _events.Add(agentEvent);
                targets = [.. _subscribers];
            }

            foreach (var target in targets)
            {
                target.Writer.TryWrite(agentEvent);
            }
        }

        public void SetCode(CodeArtifact code)
        {
            lock (_gate)
            {
                _code = Option<CodeArtifact>.Some(code);
            }
        }

        public void SetReview(ReviewResult review)
        {
            lock (_gate)
            {
                _review = Option<ReviewResult>.Some(review);
            }
        }

        public void SetGates(IReadOnlyList<GateResult> gates)
        {
            lock (_gate)
            {
                _gates = [.. gates];
            }
        }

        public void Finish(RunStatus status)
        {
            Channel<AgentEvent>[] targets;

            lock (_gate)
            {
                _status = status;
                targets = [.. _subscribers];
                _subscribers.Clear();
            }

            foreach (var target in targets)
            {
                target.Writer.TryComplete();
            }
        }

        public (IReadOnlyList<AgentEvent> Replay, ChannelReader<AgentEvent>? Live) Subscribe()
        {
            lock (_gate)
            {
                var replay = _events.ToArray();
                if (_status is not RunStatus.Running)
                {
                    return (replay, null);
                }

                var channel = Channel.CreateUnbounded<AgentEvent>();
                _subscribers.Add(channel);
                return (replay, channel.Reader);
            }
        }
    }
}
