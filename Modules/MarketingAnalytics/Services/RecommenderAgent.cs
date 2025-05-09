using System.Reflection.Metadata;
using System.Threading.Tasks;
using Catalog.Models;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using SharedKernel.MathUtil;

namespace MarketingAnalytics.Services
{
    // losa ideja realno, ovo je nada da korisniku zelje su poput ravni u R^8
    public class RecommenderAgent : IRecommenderAgent
    {
        private readonly double learingRate;
        private readonly double exploreThreshold;
        public const int featureDimension = 8;
        private readonly Random random = new Random();
        private readonly object _lock = new object();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AdDbContext _context;
        private readonly CatalogDbContext _catalogDbContext;
        public RecommenderAgent(AdDbContext context, CatalogDbContext catalogDbContext, IServiceScopeFactory scopeFactory,
                            double learingRate = 0.01, double exploreThreshold = 0.1)
        {
            this.learingRate = learingRate;
            this.exploreThreshold = exploreThreshold;
            _scopeFactory = scopeFactory;
            _context = context;
            _catalogDbContext = catalogDbContext;
        }
        public async Task<List<AdFeaturePair>> RecommendAsync(string userId)
        {
            // random chacne odaberi bilo koju reklamu iz baze
            if (random.NextDouble() < exploreThreshold)
            {
                int skipper = random.Next(0, _context.Advertisments.Count());
                var randAd = _context
                        .Advertisments
                        .OrderBy(ad => Guid.NewGuid())
                        .Skip(skipper)
                        .FirstOrDefault();
                var features = await FeatureEmbedding(userId, randAd);
                return new List<AdFeaturePair> {
                    new AdFeaturePair {
                        Ad = randAd,
                        FeatureVec = features
                        }
                    };

            }

            // procjeni broj reklama sto se trb vidjeti 0+
            var dist = new LogNormal(0.2, 0.8);
            int N = (int)Math.Round(dist.Next()); // klk reklama poslati
            if (N == 0)
                return new List<AdFeaturePair>();
            // nabavi kandidate
            var limitDate = DateTime.UtcNow.AddDays(-7);
            var activities = await _context.UserActivities.
                                    Where(activity => activity.UserId == userId && activity.TimeStamp > limitDate)
                                    .ToListAsync();
            var categories = activities.Select(a => a.ProductCategoryId);
            var candidates = await _context.Advertisments.Where(ad => ad.IsActive &&
            categories.Count(c => c == ad.ProductCategoryId) > 0).ToListAsync();
            var triggers = activities.Aggregate(0, (acc, a) => acc ^ ((int)a.InteractionType));
            var c = await _context.Advertisments.Where(ad => ad.IsActive && (triggers & ad.Triggers) != 0).ToListAsync();
            candidates.AddRange(c);
            return await RecommendCandidatesAsync(userId, candidates, N);
        }

        public async Task<List<AdFeaturePair>> RecommendCandidatesAsync(string userId, List<Advertisment> candidates, int N = 1)
        {
            if (!candidates.Any())
                throw new InvalidDataException("No candidates provided");
            if (random.NextDouble() < exploreThreshold)
            {
                var randAd = candidates[random.Next(candidates.Count)];
                var features = await FeatureEmbedding(userId, randAd);
                return new List<AdFeaturePair> {
                    new AdFeaturePair {
                        Ad = randAd,
                        FeatureVec =features
                        }
                    }; // fuj
            }

            var candidateFeatures = await FeatureEmbeddingListAsync(userId, candidates);
            var weights = await GetWeights(userId);
            var scoring = candidateFeatures.Select(f => Score(f, weights)).ToList();

            List<int> topIndices = scoring
                            .Select((value, index) => new { Value = value, Index = index })
                            .OrderByDescending(x => x.Value)
                            .Take(N)
                            .Select(x => x.Index)
                            .ToList();
            var res = new List<AdFeaturePair>();
            foreach (var index in topIndices)
            {
                res.Add(new AdFeaturePair
                {
                    Ad = candidates[index],
                    FeatureVec = candidateFeatures[index]
                });
            }
            return res;
        }

        public async Task<List<double[]>> FeatureEmbeddingListAsync(string userId, List<Advertisment> ads)
        {
            var tasks = ads.Select(ad => FeatureEmbedding(userId, ad)).ToList();
            var results = await Task.WhenAll(tasks);
            var allItems = results.ToList();
            return allItems;
        }
        public async Task<List<Func<double, double>>> GetTransformFuncsAsync(string userId)
        {
            var t = new List<Func<double, double>>(featureDimension);

            for (int i = 0; i < featureDimension; i++)
            {
                t.Add(x => 0);
            }

            using var scope = _scopeFactory.CreateScope();
            Func<AdDbContext> createContext = () => scope.ServiceProvider.GetRequiredService<AdDbContext>();
            var task1 = Task.Run(async () =>
            {
                using var context = createContext();
                var normAd = new Normalizator<AdDbContext, Advertisment>(context);
                return await normAd.ZTranformFactoryAsync(
                    ad => ad.ViewPrice,
                    ad => ad.IsActive
                );
            });

            var task2 = Task.Run(async () =>
            {
                using var context = createContext();
                var normAd = new Normalizator<AdDbContext, Advertisment>(context);
                return await normAd.ZTranformFactoryAsync(
                    ad => ad.ClickPrice,
                    ad => ad.IsActive
                );
            });

            var task3 = Task.Run(async () =>
            {
                using var context = createContext();
                var normAd = new Normalizator<AdDbContext, Advertisment>(context);
                return await normAd.ZTranformFactoryAsync(
                    ad => ad.Clicks,
                    ad => ad.IsActive
                );
            });

            var task5 = Task.Run(async () =>
            {
                using var context = createContext();
                var normAd = new Normalizator<AdDbContext, Advertisment>(context);
                return await normAd.ZTranformFactoryAsync(
                    ad => ad.ConversionPrice,
                    ad => ad.IsActive
                );
            });

            var task6 = Task.Run(async () =>
            {
                using var context = createContext();
                var normAd = new Normalizator<AdDbContext, Advertisment>(context);
                return await normAd.ZTranformFactoryAsync(
                    ad => ad.Conversions,
                    ad => ad.IsActive
                );
            });
            t[0] = x => 1;
            t[4] = x => 0;
            t[7] = x => 0;
            await Task.WhenAll(task1, task2, task3, task5, task6);
            t[1] = await task1;
            t[2] = await task2;
            t[3] = await task3;
            t[5] = await task5;
            t[6] = await task6;
            return t;
        }
        public async Task<double[]> FeatureEmbedding(string userId, Advertisment ad)
        {
            // x = [ 
            // bias
            // CijenaViewa
            // CijenaKlik
            // Broj Klikova za adId
            // Broj Klikova za userId sa ad Id
            // CijenaKonverzija
            // Broj Konverzija sa adId
            // Broj Konverzija za userId sa adId
            // ]
            // ---------
            // Mathcing aktivnost (binarno) po ova zad 2 pravim kandidat listu uz vrijeme...
            // Matching pcat (binarno)
            //
            var f = new double[featureDimension];
            var transforms = await GetTransformFuncsAsync(userId);
            f[0] = 1;
            var normAd = new Normalizator<AdDbContext, Advertisment>(_context);
            f[1] = transforms[1]((double)ad.ViewPrice);
            f[2] = transforms[2]((double)ad.ClickPrice);
            f[3] = transforms[3]((double)ad.Clicks);
            // f[4] netrivijalno naci klk je za svaku reklamu dao klikova iz tog niza vadim z
            f[4] = await _context.Clicks.CountAsync(c => c.UserId == userId && c.AdvertismentId == ad.Id);
            f[4] /= await _context.Clicks.CountAsync(c => c.UserId == userId);
            f[5] = transforms[5]((double)ad.ConversionPrice);
            f[6] = transforms[6]((double)ad.Conversions);

            // f[7] netrivijalno naci klk je za svaku reklamu dao konverzija iz tog niza vadim z
            f[7] = await _context.Conversions.CountAsync(c => c.UserId == userId && c.AdvertismentId == ad.Id);
            f[7] /= await _context.Conversions.CountAsync(c => c.UserId == userId);
            return f;
        }

        public async Task<double> ScoreAd(Advertisment ad, string userId)
        {
            return await Score(await FeatureEmbedding(userId, ad), userId);
        }
        public async Task<double> Score(double[] featureVec, string userId)
        {
            var weights = await GetWeights(userId);
            double score = weights.Zip(featureVec, (x, y) => x * y).Sum();
            return score;
        }

        public double Score(double[] featureVec, double[] weights)
        {
            double score = weights.Zip(featureVec, (x, y) => x * y).Sum();
            return score;
        }

        public async Task<double[]> GetWeights(string userId)
        {
            var w = await _context.UserWeights.FirstOrDefaultAsync(w => w.UserId == userId);
            if (w != null)
                return w.Weights;
            return [
                    1.0, 0.05,
                    0.5, 0.6,
                    0.75, 1.0,
                    0.75, 1.0
                    ];
            // vidjeno interesuje je nike swoosh

        }

        public async Task SetWeights(string userId, double[] weights)
        {
            // sto ne opt 2 reda?
            var w = await _context.UserWeights.FirstOrDefaultAsync(w => w.UserId == userId);
            if (w == null)
            {
                await _context.AddAsync(new UserWeights
                {
                    Id = 0,
                    Weights = weights,
                    UserId = userId
                });

            }
            else
            {
                w.Weights = weights;
            }
            await _context.SaveChangesAsync();

        }

        // zelim feature poslan i nagradu ($)
        public async Task RecordRewardAsync(double[] featureVec, double reward, string userId)
        {

            double eval = await Score(featureVec, userId);
            double loss = reward - eval;
            var weights = await GetWeights(userId);
            weights = weights.Zip(featureVec, (x, y) => x - learingRate * loss * y).ToArray();
            await SetWeights(userId, weights);
        }
    }
}