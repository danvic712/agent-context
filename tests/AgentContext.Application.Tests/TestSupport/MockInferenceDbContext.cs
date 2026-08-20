using System.Linq.Expressions;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace AgentContext.Application.Tests.TestSupport;

internal static class MockInferenceDbContext
{
    public static Mock<AgentContextDbContext> Create(
        IEnumerable<InferenceConfiguration>? configurations = null,
        IEnumerable<InferenceProvider>? providers = null,
        IEnumerable<InferenceRoute>? routes = null,
        IEnumerable<AppSetting>? settings = null)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        context.SetupGet(db => db.InferenceConfigurations)
            .Returns(CreateDbSet(configurations ?? []));
        context.SetupGet(db => db.InferenceProviders)
            .Returns(CreateDbSet(providers ?? []));
        context.SetupGet(db => db.InferenceRoutes)
            .Returns(CreateDbSet(routes ?? []));
        context.SetupGet(db => db.AppSettings)
            .Returns(CreateDbSet(settings ?? []));
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return context;
    }

    private static DbSet<TEntity> CreateDbSet<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
    {
        var entityList = entities.ToList();
        var queryable = entityList.AsQueryable();
        var dbSet = new Mock<DbSet<TEntity>>();
        dbSet.Setup(set => set.Add(It.IsAny<TEntity>()))
            .Callback<TEntity>(entity => entityList.Add(entity));
        dbSet.Setup(set => set.Remove(It.IsAny<TEntity>()))
            .Callback<TEntity>(entity => entityList.Remove(entity));
        dbSet.As<IAsyncEnumerable<TEntity>>()
            .Setup(item => item.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => new TestAsyncEnumerator<TEntity>(queryable.GetEnumerator()));
        dbSet.As<IQueryable<TEntity>>().Setup(item => item.Provider)
            .Returns(new TestAsyncQueryProvider<TEntity>(queryable.Provider));
        dbSet.As<IQueryable<TEntity>>().Setup(item => item.Expression).Returns(queryable.Expression);
        dbSet.As<IQueryable<TEntity>>().Setup(item => item.ElementType).Returns(queryable.ElementType);
        dbSet.As<IQueryable<TEntity>>().Setup(item => item.GetEnumerator()).Returns(() => queryable.GetEnumerator());
        return dbSet.Object;
    }

    private sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
            => new TestAsyncEnumerable<TEntity>(StripEntityFrameworkMethods(expression));

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new TestAsyncEnumerable<TElement>(StripEntityFrameworkMethods(expression));

        public object? Execute(Expression expression)
            => inner.Execute(StripEntityFrameworkMethods(expression));

        public TResult Execute<TResult>(Expression expression)
            => inner.Execute<TResult>(StripEntityFrameworkMethods(expression));

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments().Single();
            var execute = typeof(IQueryProvider)
                .GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute) &&
                                  method.IsGenericMethodDefinition &&
                                  method.GetParameters().Length == 1)
                .MakeGenericMethod(resultType);
            var result = execute.Invoke(inner, [StripEntityFrameworkMethods(expression)]);
            var task = typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result]);
            return (TResult)task!;
        }

        private static Expression StripEntityFrameworkMethods(Expression expression)
            => new EntityFrameworkMethodStripper().Visit(expression)!;
    }

    private sealed class TestAsyncEnumerable<TEntity> : EnumerableQuery<TEntity>, IAsyncEnumerable<TEntity>, IQueryable<TEntity>
    {
        public TestAsyncEnumerable(IEnumerable<TEntity> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<TEntity>(this);

        public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new TestAsyncEnumerator<TEntity>(this.AsEnumerable().GetEnumerator());
    }

    private sealed class TestAsyncEnumerator<TEntity>(IEnumerator<TEntity> inner) : IAsyncEnumerator<TEntity>
    {
        public TEntity Current => inner.Current;

        public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EntityFrameworkMethodStripper : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
                node.Method.Name is "AsNoTracking" or "Include")
            {
                return Visit(node.Arguments[0]);
            }

            return base.VisitMethodCall(node);
        }
    }
}
