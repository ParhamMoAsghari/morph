using Morph.Access.Domain;

namespace Morph.Access.Evaluation;

public interface IAccessEvaluator
{
    Task<AccessDecision> EvaluateAsync(AccessContext context, CancellationToken cancellationToken = default);
}
