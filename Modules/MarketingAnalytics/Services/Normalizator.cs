using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace MarketingAnalytics.Services
{
    public class Normalizator<TContext, TEntity>
        where TContext : DbContext
        where TEntity : class
    {
        private readonly TContext _dbContext;

        public Normalizator(TContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<double> ZScore<TValue>(
               Expression<Func<TEntity, TValue>> columnSelector,
               Expression<Func<TEntity, bool>> filter,
               double x
               )
               where TValue : IConvertible
        {
            var m = await MeanAsync(columnSelector, filter);
            var s = await StdDevAsync(columnSelector, filter);
            return (x - m) / s;
        }

        public async Task<double[]> NormalizeColumnAsync<TValue>(
               Expression<Func<TEntity, TValue>> columnSelector,
               Expression<Func<TEntity, bool>> filter
               )
               where TValue : IConvertible // Ensure TValue can be converted to double
        {

            var compiledColumnSelector = columnSelector.Compile();

            // 1. Fetch the entities that match the filter
            var entitiesToNormalize = await _dbContext.Set<TEntity>()
                                                     .Where(filter)
                                                     .ToListAsync();

            if (!entitiesToNormalize.Any())
            {
                return [0];
            }

            // 2. Extract the values to be normalized and convert them to double for calculation
            var values = entitiesToNormalize
                .Select(entity => Convert.ToDouble(compiledColumnSelector(entity)))
                .ToList(); // ToList() to materialize for multiple enumerations (mean, stddev)

            if (!values.Any())
            {
                return [0];
            }

            // 3. Calculate Mean
            double mean = CalculateMean(values);

            // 4. Calculate Standard Deviation
            double standardDeviation = CalculateStandardDeviation(values, mean);


            var zs = new double[entitiesToNormalize.Count];
            for (int i = 0; i < entitiesToNormalize.Count; i++)
            {
                var entity = entitiesToNormalize[i];
                double x = Convert.ToDouble(compiledColumnSelector(entity));
                double z = (x - mean) / standardDeviation;
                zs[i] = !Equal(standardDeviation, 0) ? z : x - mean;
            }
            return zs;
        }



        public async Task<double> StdDevAsync<T>(
               Expression<Func<TEntity, T>> columnSelector,
               Expression<Func<TEntity, bool>> filter
               )
               where T : IConvertible
        {
            var compiledColumnSelector = columnSelector.Compile();

            // 1. Fetch the entities that match the filter
            var entitiesToNormalize = await _dbContext.Set<TEntity>()
                                                     .Where(filter)
                                                     .ToListAsync();

            if (!entitiesToNormalize.Any())
            {
                return 0;
            }

            // 2. Extract the values to be normalized and convert them to double for calculation
            var values = entitiesToNormalize
                .Select(entity => Convert.ToDouble(compiledColumnSelector(entity)))
                .ToList(); // ToList() to materialize for multiple enumerations (mean, stddev)

            if (!values.Any())
            {
                return 0;
            }

            // 3. Calculate Mean
            var mean = CalculateMean(values);
            return CalculateStandardDeviation(values, mean);
        }
        public async Task<double> MeanAsync<T>(
              Expression<Func<TEntity, T>> columnSelector,
              Expression<Func<TEntity, bool>> filter
              )
              where T : IConvertible
        {
            var compiledColumnSelector = columnSelector.Compile();

            // 1. Fetch the entities that match the filter
            var entitiesToNormalize = await _dbContext.Set<TEntity>()
                                                     .Where(filter)
                                                     .ToListAsync();

            if (!entitiesToNormalize.Any())
            {
                return 0;
            }

            // 2. Extract the values to be normalized and convert them to double for calculation
            var values = entitiesToNormalize
                .Select(entity => Convert.ToDouble(compiledColumnSelector(entity)))
                .ToList(); // ToList() to materialize for multiple enumerations (mean, stddev)

            if (!values.Any())
            {
                return 0;
            }

            // 3. Calculate Mean
            return CalculateMean(values);
        }

        public static bool Equal(double x, double y, double eps = 1e-9)
        {
            return Math.Abs(x - y) <= eps * (Math.Abs(x) + Math.Abs(y));
        }

        private static double CalculateMean(IEnumerable<double> values)
        {
            if (values == null || !values.Any())
            {
                return 0.0;
            }
            return values.Average();
        }

        private static double CalculateStandardDeviation(IEnumerable<double> values, double mean)
        {
            if (values == null || values.Count() < 2) // StdDev is not meaningful for 0 or 1 item for Z-score
            {
                return 0.0;
            }

            double sumOfSquaredDifferences = values.Sum(v => Math.Pow(v - mean, 2));
            int count = values.Count();
            return Math.Sqrt(sumOfSquaredDifferences / (count - 1));
        }

    }
}